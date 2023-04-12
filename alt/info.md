docker network create cassandra-docker
docker-compose down -v --rmi all


```
sudo apt update
sudo apt install docker.io
sudo usermod -aG docker $USER
newgrp docker
sudo curl -L "https://github.com/docker/compose/releases/download/1.29.2/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose
docker-compose --version
```



13.03.2023
Konsistent kann nicht wirklich gewährleistet werden wenn 2 / 3 nodes ausfallen.
und diese 2 kommen gleichzeitig hoch ist in inkonsistenter zustand. node3 hat noch alles und die zwei neuen starten bei Null
--> normal Stichwort Quorum

Wenn Sie einen 3-Knoten-Cassandra-Cluster haben und zwei Knoten gleichzeitig ausfallen, haben Sie tatsächlich einen inkonsistenten Zustand im Cluster. Das liegt daran, dass Cassandra standardmäßig auf einem Quorum-basierten Konsistenzmodell basiert, bei dem ein Schreibvorgang erfolgreich ist, wenn die Mehrheit der Replikate erfolgreich geschrieben wurde. In einem 3-Knoten-Cluster bedeutet dies, dass Sie mindestens zwei Knoten benötigen, um Schreibvorgänge durchzuführen und einen konsistenten Zustand im Cluster aufrechtzuerhalten.

Wenn Sie also zwei Knoten verlieren, können Schreibvorgänge nicht mehr erfolgreich durchgeführt werden, da die verbleibenden Knoten nicht mehr die erforderliche Mehrheit bilden, um die Konsistenz aufrechtzuerhalten. Dies führt zu einem inkonsistenten Zustand im Cluster.


--> 2 Ausfall bedarf einer Sonderbehandlung im Backend oder so 

--> wenn nur ein Datensatz vorhanden ist, wird die konsistenz automatisch wiederhergestellt
der ausgefalle node hat nicht immer die neuen Daten und startet leer
wenn der datensatz auf den node der noch da ist geupdatet wird, schreibt es die daten wieder in die db auf ALLE nodes dann passts wieder.



---



Testszenario:

