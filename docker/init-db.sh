#!/usr/bin/env bash
set -euo pipefail

sqlcmd_bin="/opt/mssql-tools18/bin/sqlcmd"
server="${SQLSERVER_HOST:-db}"
database="${BANK_DB_NAME:-BankTransferDb}"
password="${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD is required}"
schema_file="/workspace/schema.sql"

until "$sqlcmd_bin" -S "$server" -U sa -P "$password" -C -Q "SELECT 1" > /dev/null 2>&1; do
    echo "Waiting for SQL Server at $server..."
    sleep 2
done

echo "Ensuring database '$database' exists..."
"$sqlcmd_bin" -S "$server" -U sa -P "$password" -C -Q "IF DB_ID(N'$database') IS NULL CREATE DATABASE [$database];"

echo "Applying schema and seed script..."
"$sqlcmd_bin" -S "$server" -U sa -P "$password" -C -d "$database" -i "$schema_file"

echo "Database initialization completed."
