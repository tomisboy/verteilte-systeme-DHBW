namespace Client // Note: actual namespace depends on the project name.
{
    internal abstract class Program
    {
        private const int ClientStartDelay = 1000;

        public const short maxAreaX = 1000;
        public const short maxAreaY = 1000;
        public const int clientCnt = 100;

        public static Random random = new(0); 
        public static int finishedCount = 0;
        public static object finishedCountLock = new();
        
        private static void Main(string[] args)
        {
            Logger.IsDebugEnabled = false;

            SimulateClients(clientCnt);

            Console.ReadLine();
        }

        #region Private Methods

        private static void SimulateClients(int count)
        {
            for (var i = 0; i < count; i++)
            {
                var client = new Client(maxAreaX, maxAreaY, i);

                Task.Run(() => client.BeginCommunication());

                Thread.Sleep(ClientStartDelay);
            }
        }
        #endregion

    }
}