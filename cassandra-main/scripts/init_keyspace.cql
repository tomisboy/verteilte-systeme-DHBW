CREATE KEYSPACE vs WITH REPLICATION = {'class': 'SimpleStrategy', 'replication_factor': 1};
CREATE KEYSPACE vs2 WITH REPLICATION = {'class': 'SimpleStrategy', 'replication_factor': 2};

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