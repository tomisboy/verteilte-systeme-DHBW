namespace Client // Note: actual namespace depends on the project name.
{
    internal abstract class Program
    {
        private const int ClientStartDelay = 100;

        const short maxAreaX = 1000;
        const short maxAreaY = 1000;

        private static void Main(string[] args)
        {
            const int clientCnt = 3;

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