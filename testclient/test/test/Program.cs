﻿using Cassandra;

class Program
{
    static void Main(string[] args)
    {
        // Cassandra Cluster und Session erstellen
        Cluster cluster = Cluster.Builder().AddContactPoint("localhost").Build();
        ISession session = cluster.Connect("vs");

        // Insert-Statement vorbereiten
        var insertStatement = session.Prepare("INSERT INTO test (name, password) VALUES (?, ?)");

        // 100 verschiedene Datensätze als SQL-Statements hinzufügen
        for (int i = 1; i <= 100; i++)
        {
            // Parameterwerte setzen
            var statement = insertStatement.Bind("value" + i, "password" + i);

            // Statement ausführen
            session.Execute(statement);
        }
        

        // Verbindung schließen
        session.Dispose();
        cluster.Dispose();
    }
}