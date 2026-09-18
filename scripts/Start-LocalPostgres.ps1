. (Join-Path $PSScriptRoot "LocalDevelopment.Common.ps1")
Import-LocalDevelopmentEnvironment -RequireDatabasePassword

$postgresProjectPath = Get-PostgresProjectPath
$postgresBin = Get-PostgresBinDirectory $postgresProjectPath
$pgCtl = Join-Path $postgresBin "pg_ctl.exe"
$pgIsReady = Join-Path $postgresBin "pg_isready.exe"
$initDb = Join-Path $postgresBin "initdb.exe"
$psql = Join-Path $postgresBin "psql.exe"
$createdb = Join-Path $postgresBin "createdb.exe"
$dataDirectory = Join-Path $postgresProjectPath ".local-data\postgres"
$logDirectory = Join-Path $postgresProjectPath ".local-logs"
$logFile = Join-Path $logDirectory "postgres.log"

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

if (-not (Test-Path -LiteralPath (Join-Path $dataDirectory "PG_VERSION"))) {
    New-Item -ItemType Directory -Path $dataDirectory -Force | Out-Null
    $passwordFile = Join-Path $postgresProjectPath ".local-data\postgres-password.tmp"
    try {
        [System.IO.File]::WriteAllText($passwordFile, $env:POSTGRES_PASSWORD)
        $initArguments = @(
            "--pgdata=$dataDirectory",
            "--encoding=UTF8",
            "--locale=C",
            "--username=$($env:POSTGRES_USER)",
            "--pwfile=$passwordFile",
            "--auth-local=scram-sha-256",
            "--auth-host=scram-sha-256"
        )
        & $initDb @initArguments
        if ($LASTEXITCODE -ne 0) {
            throw "PostgreSQL initialization failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        if (Test-Path -LiteralPath $passwordFile) {
            Remove-Item -LiteralPath $passwordFile -Force
        }
    }
}

$env:PGPASSWORD = $env:POSTGRES_PASSWORD
$readinessOutput = & $pgIsReady "--host=127.0.0.1" "--port=$($env:POSTGRES_PORT)"
$serverIsReady = $LASTEXITCODE -eq 0

if (-not $serverIsReady) {
    $startArguments = @(
        "start",
        "--pgdata=$dataDirectory",
        "--log=$logFile",
        "--options=-p $($env:POSTGRES_PORT) -h 127.0.0.1",
        "--wait"
    )
    & $pgCtl @startArguments
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL startup failed. See $logFile"
    }
}

$queryArguments = @(
    "--host=127.0.0.1",
    "--port=$($env:POSTGRES_PORT)",
    "--username=$($env:POSTGRES_USER)",
    "--dbname=postgres",
    "--tuples-only",
    "--no-align",
    "--command=SELECT 1 FROM pg_database WHERE datname = '$($env:POSTGRES_DB)'"
)
$databaseExists = & $psql @queryArguments
if ($LASTEXITCODE -ne 0) {
    throw "Could not connect to the local PostgreSQL server."
}
if (($databaseExists | Out-String).Trim() -ne "1") {
    $createArguments = @(
        "--host=127.0.0.1",
        "--port=$($env:POSTGRES_PORT)",
        "--username=$($env:POSTGRES_USER)",
        $env:POSTGRES_DB
    )
    & $createdb @createArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Could not create database $($env:POSTGRES_DB)."
    }
}

Write-Host "PostgreSQL is ready at localhost:$($env:POSTGRES_PORT)."
Write-Host "Data directory: $(Join-Path $script:ProjectRoot '.local-data\postgres')"
