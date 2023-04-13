namespace Client // Note: actual namespace depends on the project name.
{
    internal abstract class Program
    {
        private const int ClientStartDelay = 1000;
        private static int _minutesToShutdown = 600;
        
        private static short _maxAreaX = 1000;
        private static short _maxAreaY = 1000;
        public static short ClientCnt = 1000;
        
        public static int FinishedCount = 0;
        
        public static readonly Random Random = new(0); 
        public static readonly object FinishedCountLock = new();
        
        private static void Main(string[] args)
        {
            Logger.IsDebugEnabled = false;
            
            var areaConfig = File.ReadAllLines("config/area.txt").ToList();
            _maxAreaX = short.Parse(areaConfig[0]);
            _maxAreaY = short.Parse(areaConfig[1]);
            ClientCnt = short.Parse(areaConfig[1]);
            
            // start clients
            for (var i = 0; i < ClientCnt; i++)
            {
                var client = new Client(_maxAreaX, _maxAreaY, i);

                Task.Run(() => client.BeginCommunication());

                Thread.Sleep(ClientStartDelay);
            }

            // keep Program "minutesToShutdown" alive
            while (true)
            {
                Thread.Sleep(60000);
                _minutesToShutdown--;

                if (_minutesToShutdown == 0)
                    break;
            }
        }
    }
}