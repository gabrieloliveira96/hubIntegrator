#!/bin/bash
set -e

echo "Creating databases..."

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
    DO \$\$
    BEGIN
        IF NOT EXISTS (SELECT FROM pg_database WHERE datname = 'inbound_db') THEN
            CREATE DATABASE inbound_db;
        END IF;
        IF NOT EXISTS (SELECT FROM pg_database WHERE datname = 'orchestrator_db') THEN
            CREATE DATABASE orchestrator_db;
        END IF;
        IF NOT EXISTS (SELECT FROM pg_database WHERE datname = 'outbound_db') THEN
            CREATE DATABASE outbound_db;
        END IF;
    END
    \$\$;
    
    GRANT ALL PRIVILEGES ON DATABASE inbound_db TO $POSTGRES_USER;
    GRANT ALL PRIVILEGES ON DATABASE orchestrator_db TO $POSTGRES_USER;
    GRANT ALL PRIVILEGES ON DATABASE outbound_db TO $POSTGRES_USER;
EOSQL

echo "Databases created successfully!"

