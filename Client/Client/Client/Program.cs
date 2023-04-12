namespace Client // Note: actual namespace depends on the project name.
{
    internal abstract class Program
    {
        private const string IpFamily = "192.168";
        
        const short maxAreaX = 1000;
        const short maxAreaY = 1000;

        private static void Main(string[] args)
        {
            const int clientCnt = 1;

            Logger.IsDebugEnabled = false;

            SimulateClients(clientCnt).GetAwaiter().GetResult();

            Console.ReadLine();
        }

        #region Private Methods
        
        private static async Task SimulateClients(int count)
        {
            var clients = new List<Client>();

            for (var i = 0; i < count; i++)
            {
                var client = new Client(maxAreaX, maxAreaY, i);
                clients.Add(client);
            }

            await Task.WhenAll(clients.Select(c => Task.Run(() => c.BeginCommunication())));
        }
        #endregion
        
    }
}