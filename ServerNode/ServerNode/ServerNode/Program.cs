using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.Json;
using ServerNode.TrafficControl;

namespace ServerNode
{
    internal abstract class Program
    {
        #region Fields

        private const int TimeToCheckResponse = 10;
        private const int OpenPort = 8888;
        
        private static int _roundRobin;
        private static Object _roundRobinLock = new();

        private static readonly IPAddress _myIp = GetLocalIpAddress();
        private static List<string> _slaveIpList;
        
        private static readonly List<Socket> _masterNodes = new (); // nodes which local node is connected to

        private static TrafficControlLogic _trafficControlLogic = null!;
        const short maxAreaX = 1000;
        const short maxAreaY = 1000;
        const short maxCarsPerNode = 2;

        #endregion
        
        private static void Main()
        {
            Logger.IsLoggerEnabled = true;
            Logger.IsDebugEnabled = false;
            _slaveIpList = File.ReadAllLines("ips.txt").ToList();
            
            
            LoadTrafficState(); //Load last traffic state or create new
            
            OpenSocket(_myIp.ToString());
            
            foreach (var slave in _slaveIpList)
                TryConnectToSocket(slave);
            
            // keep Program "secondsToShutdown" alive
            var secondsToShutdown = 3000;
            while (true)
            {
                Thread.Sleep(1000);
                secondsToShutdown--;

                if (secondsToShutdown == 0)
                    break;
            }
        }

        #region Constructor / Destructor

        static Program()
        {
            Logger.HighlightMessage($"Node {_myIp} online");
            AppDomain.CurrentDomain.ProcessExit += StaticClass_Dtor;
        }
        
        static void StaticClass_Dtor(object? sender, EventArgs e) 
        {
            Logger.ErrorMessage($"Node {_myIp} offline");
        }

        #endregion

        #region Private Methods
        private static void OpenSocket(string ipAddress)
        {
            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                
                var address = IPAddress.Parse(ipAddress);
                var endPoint = new IPEndPoint(address, OpenPort);

                socket.Bind(endPoint);
                socket.Listen(100);

                Logger.InfoMessage($"Socket listening on {ipAddress}:{OpenPort}");
                
                // Spawn a new thread to handle each incoming connection
                Task.Run(() =>
                {
                    while (true)
                    {
                        var clientSocket = socket.Accept();
                        var ip = GetAddressFromRemoteEndpoint(clientSocket);

                        // Client connected to socket
                        if (!_slaveIpList.Contains(ip))
                        {
                            Task.Run(() => ListenToClientSocket(clientSocket));
                            continue;
                        }
                        
                        Logger.InfoMessage($"Slave connected: {clientSocket.RemoteEndPoint}");

                        //listen to client on own socket
                        Task.Run(() => ListenToSocket(clientSocket));
                        
                        // connect to slaves own socket
                        lock (_masterNodes)
                        {
                            if (_masterNodes.Exists(s => GetAddressFromRemoteEndpoint(s) == GetAddressFromRemoteEndpoint(clientSocket)))
                                continue;
                            
                            Task.Run(() => TryConnectToSocket(GetAddressFromRemoteEndpoint(clientSocket)));
                        }
                    }
                });
            }
            catch (Exception e)
            {
                Logger.ErrorMessage(e.Message);
            }
        }
        
        private static void TryConnectToSocket(string ip)
        {
            if (ip == _myIp.ToString())
                return;
            
            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var address = IPAddress.Parse(ip);
                var endPoint = new IPEndPoint(address, OpenPort);
            
                socket.Connect(endPoint);
            
                Logger.InfoMessage($"Connected to master: {socket.RemoteEndPoint}");
                
                Task.Run(() => ListenToSocket(socket));

                lock(_masterNodes)
                    _masterNodes.Add(socket);
            }
            catch (Exception)
            {
                Logger.ErrorMessage($"Could not connect to: {ip}");
            }
        }
        
        private static async Task ListenToSocket(Socket socket)
        {
            try
            {
                var buffer = new byte[1024];
                var sb = new StringBuilder();
                
                while (socket.Connected)
                {
                    var bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                    var receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                    sb.Append(receivedData);
                    
                    if (receivedData.Contains("Change"))
                    {
                        var parts = receivedData.Split(new char[] { ':', '-', '[', ']', ';' }, StringSplitOptions.RemoveEmptyEntries);

                        var id = short.Parse(parts[1]);
                        var newPosParts = parts[2].Split(',');
                        var newPos = new Coordinate(short.Parse(newPosParts[0]), short.Parse(newPosParts[1]));
                        
                        lock (_trafficControlLogic)
                        {
                            var pos = _trafficControlLogic.TrafficArea.GetPosition(id);
                            
                            if(pos == null) // add if new car
                                _trafficControlLogic.TrafficArea.Place(id, newPos);
                            else if (pos != newPos) // update pos if old car
                            {
                                _trafficControlLogic.TrafficArea.Remove(id, pos);
                                _trafficControlLogic.TrafficArea.Place(id, newPos);
                            }
                        }
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
                lock (_masterNodes)
                    _masterNodes.Remove(socket);

                Logger.ErrorMessage($"Connection to {GetAddressFromRemoteEndpoint(socket)} lost");
            }
        }

        private static async void ListenToClientSocket(Socket socket)
        {
            try
            {
                var buffer = new byte[1024];
                var sb = new StringBuilder();
                
                Logger.HighlightMessage($"Client {socket.RemoteEndPoint} connected.");
                 
                while (socket.Connected)
                {
                    var bytesRead = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                    var receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                    sb.Append(receivedData);
                    
                    if (receivedData.Contains("Help")) // Client asks for navigation
                    {
                        var rr = GetNextRoundRobinId();
                        
                        if (_slaveIpList[rr] == _myIp.ToString())
                                SendTcp(socket, "Accepted");
                        else
                        {
                            SendTcp(socket, $"{_slaveIpList[rr]}");
                            Logger.InfoMessage($"Connection with {GetAddressFromRemoteEndpoint(socket)} ended.");
                            break;
                        }
                    }
                    else if (receivedData.Contains("Sudo help")) // Client demands navigation
                    {
                        SendTcp(socket, "Accepted");
                    }
                    else if (receivedData.Contains("Req")) // Client requests next step
                    {
                        var newPos = CalculateMove(receivedData);
                        
                        var id = short.Parse(receivedData.Split(new[] { ':', '-', '[', ']', ';' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                        
                        var messageToNodes = $"Change:{id}-[{newPos.X},{newPos.Y}];";
                        var messageToClient = $"Asw:[{newPos.X},{newPos.Y}];";
                        
                        SendTcp(socket, messageToClient);
                        SendChangeToNodes(messageToNodes);
                        
                        Logger.InfoMessage(messageToClient);
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
            }
        }

        private static Coordinate CalculateMove(string request)
        {
            Coordinate nextMove;
            var parts = request.Split(new[] { ':', '-', '[', ']', ';' }, StringSplitOptions.RemoveEmptyEntries);

            var id = short.Parse(parts[1]);
            var fromPosParts = parts[2].Split(',');
            var fromPos = new Coordinate(short.Parse(fromPosParts[0]), short.Parse(fromPosParts[1]));

            var toPosParts = parts[3].Split(',');
            var toPos = new Coordinate(short.Parse(toPosParts[0]), short.Parse(toPosParts[1]));
            
            try
            {
                lock (_trafficControlLogic)
                {
                    var pos = _trafficControlLogic.TrafficArea.GetPosition(id);
                
                    if(pos == null) // add if new car
                        _trafficControlLogic.TrafficArea.Place(id, fromPos);
                    else if (pos != fromPos) // update pos if old car
                    {
                        _trafficControlLogic.TrafficArea.Remove(id, pos);
                        _trafficControlLogic.TrafficArea.Place(id, fromPos);
                    }

                    nextMove = _trafficControlLogic.Move(id, toPos);
                }
            
                return nextMove;
            }
            catch
            {
                return fromPos;
            }
        }

        private static int GetNextRoundRobinId()
        {
            lock (_roundRobinLock)
            {
                while (true)
                {
                    if (_roundRobin >= _slaveIpList.Count - 1)
                        _roundRobin = 0;
                    else
                        _roundRobin++;
                
                    lock (_masterNodes)
                    {
                        if (_slaveIpList[_roundRobin] == _myIp.ToString())
                            return _roundRobin;
                        
                        if (_masterNodes.Exists(s =>
                                GetAddressFromRemoteEndpoint(s) == _slaveIpList[_roundRobin]))
                            return _roundRobin;
                    }
                }
            }
        }

        private static void LoadTrafficState()
        {
            //_trafficControlLogic = new TrafficControlLogic(LoadTrafficAreaState());
            _trafficControlLogic = new TrafficControlLogic(new TrafficArea(maxCarsPerNode, maxAreaX, maxAreaY));
        }
        
        private static void SendChangeToNodes(string message)
        {
            lock (_masterNodes)
            {
                foreach (var node in _masterNodes)
                {
                    try
                    {
                        SendTcp(node, message);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        private static async void SendTcp(Socket socket, string message)
        {
            if (!socket.Connected)
                throw new SocketException();
            
            var messageBuffer = Encoding.ASCII.GetBytes($"{message}").ToArray();
            await socket.SendAsync(messageBuffer);
        }

        private static IPAddress GetLocalIpAddress()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().Contains("127.0.1.1") && !ip.ToString().Contains("0.0.0.0"))
                    return ip;
            }
            
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }
        
        private static string GetAddressFromRemoteEndpoint(Socket socket)
        {
            return ((IPEndPoint) (socket.RemoteEndPoint!)).Address.ToString();
        }

        #endregion
    }
}