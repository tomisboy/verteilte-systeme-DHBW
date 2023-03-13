using Cassandra;

class Program
{
    static void Main(string[] args)
    {
        // Cassandra Cluster und Session erstellen
        // Cluster cluster = Cluster.Builder().AddContactPoint("localhost").Build();
        Cluster cluster = Cluster.Builder().AddContactPoint("193.196.55.166").Build();
        ISession session = cluster.Connect("vs2");

        // Insert-Statement vorbereiten
        var insertStatement = session.Prepare("INSERT INTO test (name, password) VALUES (?, ?)");

        // 100 verschiedene Datensätze als SQL-Statements hinzufügen
        for (int i = 1; i <= 20;  i++)
        {
            // Parameterwerte setzen
            var statement = insertStatement.Bind("tot" + i, "password-to3" + i);

            // Statement ausführen
            session.Execute(statement);
        }
        

        // Verbindung schließen
        session.Dispose();
        cluster.Dispose();
    }
}