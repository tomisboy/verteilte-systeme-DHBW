using Cassandra;
using Newtonsoft.Json;

internal class Program
{
    private static void Main(string[] args)
    {
        var random = new Random();
        var result = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 4)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        Console.WriteLine(result);

        // Cassandra Cluster und Session erstellen
        // Cluster cluster = Cluster.Builder().AddContactPoint("localhost").Build();
        var cluster = Cluster.Builder().AddContactPoint("193.196.54.176").Build();
        //Cluster cluster = Cluster.Builder().AddContactPoint("cassandra").Build();
        var session = cluster.Connect("vs2");

        //// Insert-Statement vorbereiten
        //var insertStatement = session.Prepare("INSERT INTO test (name, password) VALUES (?, ?)");
        //
        //// 100 verschiedene Datensätze als SQL-Statements hinzufügen
        //for (int i = 1; i <= 1;  i++)
        //{
        //    // Parameterwerte setzen
        //    var statement = insertStatement.Bind("init", "altered" + i +result);

        //    // Statement ausführen
        //    session.Execute(statement);
        //}

        var insert = 0;
        if (insert == 1)
        {
            Console.WriteLine("insert");
            var insertStatement = session.Prepare("INSERT INTO test (name, password) VALUES (?, ?)");

            //    // Parameterwerte setzen
            var statement = insertStatement.Bind("init", "insert-" + result);

            //    // Statement ausführen
            session.Execute(statement);
        }
        else
        {
            Console.WriteLine("update");
            var updateStatement = session.Prepare("UPDATE test SET password = ? WHERE name = ?");

            // the parameters to the statement
            var alteredPassword = "altered-" + result;
            var name = "init";
            session.Execute(updateStatement.Bind(alteredPassword, name));
        }


        var json = JsonConvert.SerializeObject(new
        {
            isbn = "123456" + result,
            title = "titel" + result,
            publisher = "pup" + result
        });

// Erstellen Sie eine CQL-Anweisung zum Einfügen der JSON in eine Tabelle
        var insertQuery = new SimpleStatement("INSERT INTO json (id, data) VALUES (?, ?)", "json-object", json);
        session.Execute(insertQuery);
        
        session.Dispose();
        cluster.Dispose();
    }
}