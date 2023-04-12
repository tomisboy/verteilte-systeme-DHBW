using System.Net;
using System.Net.Sockets;
using System.Text;
using Client.TrafficControl;

namespace Client;

public class Client
{
    #region Fields
    
    private const int RequestDelay = 100;

    private const int TimeToCheckResponse = 10;
    private const int TimeToCheckServer = 100;
    
    private const string IpFamily = "192.168";
    private static readonly List<string> _serverIps = new (){ $"{IpFamily}.2.154", $"{IpFamily}.2.155", $"{IpFamily}.2.156" };
    private const int OpenPort = 8888;

    private Coordinate _currentPos;
    private Coordinate _targetPos;

    private DateTime _start;

    private int ID = -1;

    #endregion
    
    #region Constructors

    public Client(short maxX, short maxY, int id)
    {
        var r = new Random();
        
        _start = DateTime.Now;

        _currentPos = new Coordinate((short) r.Next(maxX), (short) r.Next(maxY));
        _targetPos = new Coordinate((short) r.Next(maxX), (short) r.Next(maxY));

        ID = id;
    }

    #endregion

    #region Public Methods

    public async void BeginCommunication()
    {
        var ipTarget = 0;

        var res = await ConnectToServer(_serverIps[ipTarget], false);
        
        while (!res)
        {
            Thread.Sleep(TimeToCheckServer);
            
            ipTarget++;
            
            res = await ConnectToServer(_serverIps[ipTarget], false);
        }
    }

    #endregion

    #region Private Methods

    private async Task<bool> ConnectToServer(string ip, bool force)
    {
        try
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var address = IPAddress.Parse(ip);

            var endPoint = new IPEndPoint(address, OpenPort);
            
            await socket.ConnectAsync(endPoint);
            
            Logger.InfoMessage($"Connected to master: {socket.RemoteEndPoint}");

            return await ListenToSocket(socket, force);
        }
        catch
        {
            Logger.ErrorMessage($"Can't connect to {ip} lost");
            return false;
        }
    }

    private async Task<bool> ListenToSocket(Socket socket, bool force)
    {
        try
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
                    
                if (receivedData.Contains("Accepted")) // send first navigation request
                {
                    var message = $"Req:{ID}-[{_currentPos.X},{_currentPos.Y}]-[{_targetPos.X},{_targetPos.Y}];";
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
                        Logger.HighlightMessage($"{ID} reached destination {(DateTime.Now - _start).TotalSeconds}");
                        return true;
                    }

                    _currentPos = answerPos;
                    
                    Thread.Sleep(RequestDelay);
                        
                    var message = $"Req:{ID}-[{_currentPos.X},{_currentPos.Y}]-[{_targetPos.X},{_targetPos.Y}];";
                    await SendTcp(socket, message);
                    Logger.InfoMessage(message);
                }
                else
                {
                    await ConnectToServer(receivedData, true); // connect to new IP
                    break;
                }

                if (sb.Length > 0)
                {
                    Logger.TcpInMessage(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    await Task.Delay(TimeToCheckResponse);
                        
                    // connection lost
                    var testBuffer = new byte[1];
                    if (socket.Poll(0, SelectMode.SelectRead) && socket.Receive(testBuffer, SocketFlags.Peek) == 0)
                        throw new SocketException();
                }
            }
        }
        catch (SocketException)
        {
            Logger.ErrorMessage($"Connection to {GetAddressFromRemoteEndpoint(socket)} lost");
            return false;
        }

        return true;
    }
    
    private static string GetAddressFromRemoteEndpoint(Socket socket)
    {
        return ((IPEndPoint) (socket.RemoteEndPoint!)).Address.ToString();
    }

    private static async Task SendTcp(Socket socket, string message)
    {
        if (!socket.Connected)
            throw new SocketException();
            
        var messageBuffer = Encoding.ASCII.GetBytes($"{message}").ToArray();
        await socket.SendAsync(messageBuffer);
    }

    #endregion
}