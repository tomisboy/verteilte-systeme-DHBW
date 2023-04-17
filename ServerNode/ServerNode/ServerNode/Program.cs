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
        
        private static int _minutesToShutdown = 600;
        private static int _roundRobin;
        
        private static short _maxAreaX;
        private static short _maxAreaY;
        private static short _maxCarsPerNode;
        
        private static IPAddress _myIp = null!;
        private static List<string> _slaveIpList = null!;
        
        private static readonly object _roundRobinLock = new(); // semaphore for round robin calculation
        private static readonly List<Socket> _masterNodes = new (); // nodes which local node is connected to

        private static TrafficControlLogic _trafficControlLogic = null!;

        #endregion
        private static void Main()
        {
            InitNodeConfig(); // initialize important node variables
            
            OpenSocket(_myIp.ToString());
            Logger.HighlightMessage($"Node {_myIp} online");
            
            foreach (var slave in _slaveIpList)
                TryConnectToSocket(slave);
            
            // keep Program "minutesToShutdown" alive
            while (true)
            {
                Thread.Sleep(60000);
                _minutesToShutdown--;

                if (_minutesToShutdown == 0)
                    break;
            }
        }

        #region Constructor / Destructor

        static Program()
        {
            Console.WriteLine(_myIp);
            AppDomain.CurrentDomain.ProcessExit += StaticClass_Dtor;
        }
        
        static void StaticClass_Dtor(object? sender, EventArgs e) 
        {
            Logger.ErrorMessage($"Node {_myIp} offline");
        }

        #endregion

        #region Private Methods

        private static void InitNodeConfig()
        {
            Logger.IsLoggerEnabled = true;
            Logger.IsDebugEnabled = false;
            
            _slaveIpList = File.ReadAllLines("config/ips.txt").ToList();
            _myIp = IPAddress.Parse(File.ReadAllLines("config/ownip.txt").ToList()[0]);
            
            var areaConfig = File.ReadAllLines("config/area.txt").ToList();

            _maxAreaX = short.Parse(areaConfig[0]);
            _maxAreaY = short.Parse(areaConfig[1]);
            _maxCarsPerNode = short.Parse(areaConfig[2]);
            
            _trafficControlLogic = new TrafficControlLogic(new TrafficArea(_maxCarsPerNode, _maxAreaX, _maxAreaY));
        }
        
        /// <summary>
        /// Opens a new socket connection on given IPAddress
        /// </summary>
        private static void OpenSocket(string ipAddress)
        {
            try
            {
                //open socket on own IpAddress
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                
                var address = IPAddress.Parse(ipAddress);
                var endPoint = new IPEndPoint(address, OpenPort);

                socket.Bind(endPoint);
                socket.Listen(1000);

                Logger.InfoMessage($"Socket listening on {ipAddress}:{OpenPort}");
                
                // Spawn a new thread to handle each incoming connection
                Task.Run(() => HandleIncomingConnections(socket));
            }
            catch (Exception e)
            {
                Logger.ErrorMessage(e.Message);
            }
        }

        /// <summary>
        /// Handles incoming connections from given socket. (Method runs as long as node is alive)
        /// </summary>
        private static void HandleIncomingConnections(Socket socket)
        {
            while (true)
            {
                // accept new incoming connections
                var clientSocket = socket.Accept();
                var ip = GetAddressFromRemoteEndpoint(clientSocket);

                // Client connected to socket
                if (!_slaveIpList.Contains(ip))
                {
                    Task.Run(() => ListenToClientSocket(clientSocket));
                    continue;
                }
                        
                Logger.InfoMessage($"Slave connected: {clientSocket.RemoteEndPoint}");

                //listen to other node on own socket
                Task.Run(() => ListenToNodeSocket(clientSocket));
                        
                // connect to slaves own socket
                lock (_masterNodes)
                {
                    if (_masterNodes.Exists(s => GetAddressFromRemoteEndpoint(s) == GetAddressFromRemoteEndpoint(clientSocket)))
                        continue;
                            
                    Task.Run(() => TryConnectToSocket(GetAddressFromRemoteEndpoint(clientSocket)));
                }
            }
        }
        
        /// <summary>
        /// Try connect to a given socket from remoteIp
        /// </summary>
        private static void TryConnectToSocket(string remoteIp)
        {
            if (remoteIp == _myIp.ToString())
                return;
            
            try
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var address = IPAddress.Parse(remoteIp);
                var endPoint = new IPEndPoint(address, OpenPort);
            
                socket.Connect(endPoint);
            
                Logger.InfoMessage($"Connected to master: {socket.RemoteEndPoint}");
                
                Task.Run(() => ListenToNodeSocket(socket));

                lock(_masterNodes)
                    _masterNodes.Add(socket);
            }
            catch (Exception)
            {
                Logger.ErrorMessage($"Could not connect to: {remoteIp}");
            }
        }
        
        /// <summary>
        /// Listens to all incoming messages from a node socket.
        /// </summary>
        private static async Task ListenToNodeSocket(Socket socket)
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

                    HandleNodeRequest(receivedData); // handle incoming node request
                    
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
            }
            catch (SocketException)
            {
                lock (_masterNodes)
                    _masterNodes.Remove(socket);

                Logger.ErrorMessage($"Connection to {GetAddressFromRemoteEndpoint(socket)} lost");
            }
        }

        /// <summary>
        /// Listens to all incoming messages from a client socket.
        /// </summary>
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
                    
                    if(!HandleClientRequest(socket, receivedData)) // handle incoming client request
                        break;

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
            }
            catch (SocketException)
            {
                Logger.ErrorMessage($"Connection to {GetAddressFromRemoteEndpoint(socket)} lost");
            }
        }

        /// <summary>
        /// Calculates next move for a navigation request and updates TrafficControlLogic.
        /// </summary>
        private static Coordinate CalculateMove(string request)
        {
            var parts = request.Split(new[] { ':', '-', '[', ']', ';' }, StringSplitOptions.RemoveEmptyEntries);

            var id = short.Parse(parts[1]);
            var fromPosParts = parts[2].Split(',');
            var fromPos = new Coordinate(short.Parse(fromPosParts[0]), short.Parse(fromPosParts[1]));

            var toPosParts = parts[3].Split(',');
            var toPos = new Coordinate(short.Parse(toPosParts[0]), short.Parse(toPosParts[1]));
            
            try
            {
                Coordinate nextMove;
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
        
        /// <summary>
        /// Handles incoming Node Requests. Updates TrafficControlLogic with given data.
        /// </summary>
        private static void HandleNodeRequest(string data)
        {
            if (!data.Contains("Change"))
                return;
            
            var parts = data.Split(new[] { ':', '-', '[', ']', ';' }, StringSplitOptions.RemoveEmptyEntries);

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
        
        /// <summary>
        /// Handles incoming Client Requests. Updates TrafficControlLogic with given data.
        /// </summary>
        private static bool HandleClientRequest(Socket socket, string data)
        {
            if (data.Contains("Help")) // Client asks for navigation
            {
                var roundRobinIp = GetNextRoundRobinIp();
                
                //check with round robin who should handle request
                if (roundRobinIp == _myIp.ToString()) 
                    SendTcp(socket, "Accepted");
                else
                {
                    SendTcp(socket, $"{roundRobinIp}");
                    Logger.InfoMessage($"Connection with {GetAddressFromRemoteEndpoint(socket)} ended.");
                    return false;
                }
            }
            else if (data.Contains("Sudo help")) // Client demands navigation
            {
                SendTcp(socket, "Accepted");
            }
            else if (data.Contains("Req")) // Client requests next step
            {
                var newPos = CalculateMove(data);
                        
                var id = short.Parse(data.Split(new[] { ':', '-', '[', ']', ';' }, StringSplitOptions.RemoveEmptyEntries)[1]);
                        
                var messageToNodes = $"Change:{id}-[{newPos.X},{newPos.Y}];";
                var messageToClient = $"Asw:[{newPos.X},{newPos.Y}];";
                        
                SendTcp(socket, messageToClient);
                SendChangeToNodes(messageToNodes);
                        
                Logger.InfoMessage(messageToClient);
            }

            return true;
        }

        /// <summary>
        /// Returns IP for next node to handle requests.
        /// </summary>
        private static string GetNextRoundRobinIp()
        {
            lock (_roundRobinLock)
            {
                while (true)
                {
                    //increment counter
                    if (_roundRobin >= _slaveIpList.Count - 1)
                        _roundRobin = 0;
                    else
                        _roundRobin++;
                
                    lock (_masterNodes)
                    {
                        // check if own ip
                        if (_slaveIpList[_roundRobin] == _myIp.ToString())
                            return _slaveIpList[_roundRobin];
                        
                        // check if next node is online
                        if (_masterNodes.Exists(s => GetAddressFromRemoteEndpoint(s) == _slaveIpList[_roundRobin]))
                            return _slaveIpList[_roundRobin];
                    }
                }
            }
        }
        
        /// <summary>
        /// Sends message to all connected nodes.
        /// </summary>
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

        /// <summary>
        /// Sends message on specific socket.
        /// </summary>
        private static async void SendTcp(Socket socket, string message)
        {
            try
            {
                if (!socket.Connected)
                    throw new SocketException();
                
                var messageBuffer = Encoding.ASCII.GetBytes($"{message}").ToArray();
                await socket.SendAsync(messageBuffer);
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Returns IP address from remoteEndpoint without Port.
        /// </summary>
        private static string GetAddressFromRemoteEndpoint(Socket socket)
        {
            return ((IPEndPoint) socket.RemoteEndPoint!).Address.ToString();
        }

        #endregion
    }
}