using Cassandra;

class Program
{
    static void Main(string[] args)
    {
        
        var random = new Random();
        var result = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 4).Select(s => s[random.Next(s.Length)]).ToArray());
        Console.WriteLine(result);
        
        // Cassandra Cluster und Session erstellen
        // Cluster cluster = Cluster.Builder().AddContactPoint("localhost").Build();
        Cluster cluster = Cluster.Builder().AddContactPoint("193.196.55.166").Build();
        ISession session = cluster.Connect("vs2");

        // Insert-Statement vorbereiten
        var insertStatement = session.Prepare("INSERT INTO test (name, password) VALUES (?, ?)");

        // 100 verschiedene Datensätze als SQL-Statements hinzufügen
        for (int i = 1; i <= 10;  i++)
        {
            // Parameterwerte setzen
            var statement = insertStatement.Bind(result + i, "password-to3" + i);

            // Statement ausführen
            session.Execute(statement);
        }
        

        // Verbindung schließen
        session.Dispose();
        cluster.Dispose();
    }
}