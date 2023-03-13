docker network create cassandra-docker
docker-compose down -v --rmi all


13.03.2023
Konsistent kann nicht wirklich gewährleistet werden wenn 2 / 3 nodes ausfallen.
und diese 2 kommen gleichzeitig hoch ist in inkonsistenter zustand. node3 hat noch alles und die zwei neuen starten bei Null
--> normal Stichwort Quorum

Wenn Sie einen 3-Knoten-Cassandra-Cluster haben und zwei Knoten gleichzeitig ausfallen, haben Sie tatsächlich einen inkonsistenten Zustand im Cluster. Das liegt daran, dass Cassandra standardmäßig auf einem Quorum-basierten Konsistenzmodell basiert, bei dem ein Schreibvorgang erfolgreich ist, wenn die Mehrheit der Replikate erfolgreich geschrieben wurde. In einem 3-Knoten-Cluster bedeutet dies, dass Sie mindestens zwei Knoten benötigen, um Schreibvorgänge durchzuführen und einen konsistenten Zustand im Cluster aufrechtzuerhalten.

Wenn Sie also zwei Knoten verlieren, können Schreibvorgänge nicht mehr erfolgreich durchgeführt werden, da die verbleibenden Knoten nicht mehr die erforderliche Mehrheit bilden, um die Konsistenz aufrechtzuerhalten. Dies führt zu einem inkonsistenten Zustand im Cluster.


--> 2 Ausfall bedarf einer Sonderbehandlung im Backend oder so 