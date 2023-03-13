CREATE KEYSPACE vs WITH REPLICATION = {'class': 'SimpleStrategy', 'replication_factor': 2};
CREATE KEYSPACE vs2 WITH REPLICATION = {'class': 'SimpleStrategy', 'replication_factor': 3};

CREATE TABLE vs2.test (
    name text,
    password text,
    PRIMARY KEY (name)
);



CREATE TABLE vs.test (
    name text,
    password text,
    PRIMARY KEY (name)
);