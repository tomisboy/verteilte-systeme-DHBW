using System.Net;
using System.Net.Sockets;
using System.Text;
using Client.TrafficControl;

namespace Client;

public class Client
{
    #region Fields
    
    private int _requestCount = 1;
    
    private const int RequestDelay = 20;
    private const int MaxRequestPerNode = 10000;

    private const int TimeToCheckResponse = 10;
    private const int TimeToCheckServer = 100;
    
    private const int OpenPort = 8888;
    
    private static List<string> _serverIps = null!;

    private Coordinate _currentPos;
    
    private readonly Coordinate _targetPos;
    private readonly DateTime _start;
    private readonly int _id;

    #endregion
    
    #region Constructors

    public Client(short maxX, short maxY, int id)
    {
        _start = DateTime.Now;
        
        //init client with random start and target
        lock (Program.Random)
        {
           var r = Program.Random; 
           
           _serverIps = File.ReadAllLines("config/ips.txt").ToList();
           _currentPos = new Coordinate((short) r.Next(maxX), (short) r.Next(maxY));
           _targetPos = new Coordinate((short) r.Next(maxX), (short) r.Next(maxY));
           
           _id = id; 
        }
    }

    #endregion

    #region Public Methods
    
    /// <summary>
    /// Begin communication with server nodes by picking IP. Try all Ips until connection is established.
    /// </summary>
    public async void BeginCommunication()
    {
        var ipTarget = 0;

        var res = false;
        
        while (!res)
        {
            try
            {
                Logger.InfoMessage(_serverIps[ipTarget]);
                
                if (ipTarget >= _serverIps.Count - 1)
                    ipTarget = 0;
                else
                    ipTarget++;
                
                //connect to server
                res = await ConnectToServer(_serverIps[ipTarget], false);
                
                Thread.Sleep(TimeToCheckServer);
            }
            catch // connection denied or crashed
            {
                res = false;
            }
        }
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Connect to server socket with ip. If Force is false, the server node can deny request.
    /// </summary>
    private async Task<bool> ConnectToServer(string ip, bool force)
    {
        try
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var address = IPAddress.Parse(ip);

            var endPoint = new IPEndPoint(address, OpenPort);
            
            await socket.ConnectAsync(endPoint);
            
            Logger.InfoMessage($"Connected to master: {socket.RemoteEndPoint}");

            return await ListenToServerSocket(socket, force);
        }
        catch (SocketOverloadException)
        {
            throw new SocketOverloadException();
        }
        catch
        {
            Logger.ErrorMessage($"Can't connect to {ip} lost");
            throw new SocketException();
        }
        
    }

    /// <summary>
    /// Listen to server socket
    /// </summary>
    private async Task<bool> ListenToServerSocket(Socket socket, bool force)
    {
        
        var buffer = new byte[1024];
        var sb = new StringBuilder();
        
        await SendTcp(socket, !force ? "Help" : "Sudo help"); // Ask or force navigation
            
        while (socket.Connected)
        {
            // Ask for navigation
            var bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
            var receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);

            sb.Append(receivedData);
            
            // possible answers
            if (receivedData.Contains("Accepted")) // send first navigation request
            {
                var message = $"Req:{_id}-[{_currentPos.X},{_currentPos.Y}]-[{_targetPos.X},{_targetPos.Y}];";
                await SendTcp(socket, message);
                Logger.InfoMessage(message);
            }
            else if (receivedData.Contains("Asw")) // handle incoming navigation
            {
                Logger.TcpInMessage(receivedData);
                
                var parts = receivedData.Split(new[] { ':', '[', ']', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var toPos = parts[1].Split(',');
                
                var answerPos = new Coordinate(short.Parse(toPos[0]), short.Parse(toPos[1]));

                if (answerPos.X == _targetPos.X && answerPos.Y == _targetPos.Y)
                {
                    lock (Program.FinishedCountLock)
                    {
                        socket.Disconnect(false);
                        Program.FinishedCount++;
                        Logger.HighlightMessage($"{_id} reached destination {(DateTime.Now - _start).TotalSeconds} [{Program.FinishedCount}/{Program.ClientCnt}]");
                    }
                    return true;
                }

                _currentPos = answerPos;
                
                Thread.Sleep(RequestDelay);

                if (_requestCount % MaxRequestPerNode == 0) // reconnect after MaxRequestPerNode inorder to rebalance 
                {
                    _requestCount = 1; 
                    Logger.InfoMessage("MaxRequestPerNode reached, reconnecting");
                    throw new SocketOverloadException();
                }

                var message = $"Req:{_id}-[{_currentPos.X},{_currentPos.Y}]-[{_targetPos.X},{_targetPos.Y}];";
                _requestCount++;
                await SendTcp(socket, message);
                Logger.InfoMessage(message);
            }
            else
            {
                await ConnectToServer(receivedData, true); // connect to new IP
                break;
            }

            if (sb.Length > 0) // Log incoming message
            {
                Logger.TcpInMessage(sb.ToString());
                sb.Clear();
            }
            else
            {
                await Task.Delay(TimeToCheckResponse);
                    
                // check if connection lost
                var testBuffer = new byte[1];
                if (socket.Poll(0, SelectMode.SelectRead) && socket.Receive(testBuffer, SocketFlags.Peek) == 0)
                    throw new SocketException();
            }
        }

        return true;
    }

    /// <summary>
    /// Sends message on specific socket.
    /// </summary>
    private static async Task SendTcp(Socket socket, string message)
    {
        if (!socket.Connected)
            throw new SocketException();
        
        var messageBuffer = Encoding.ASCII.GetBytes($"{message}").ToArray();
        await socket.SendAsync(messageBuffer);
    }

    #endregion
}

/// <summary>
/// Custom exception to handle socket overload
/// </summary>
public class SocketOverloadException : Exception
{
}