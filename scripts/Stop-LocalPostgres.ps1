. (Join-Path $PSScriptRoot "LocalDevelopment.Common.ps1")
Import-LocalDevelopmentEnvironment -RequireDatabasePassword

$postgresProjectPath = Get-PostgresProjectPath
$pgCtl = Join-Path (Get-PostgresBinDirectory $postgresProjectPath) "pg_ctl.exe"
$dataDirectory = Join-Path $postgresProjectPath ".local-data\postgres"

if (-not (Test-Path -LiteralPath (Join-Path $dataDirectory "PG_VERSION"))) {
    Write-Host "The project-local PostgreSQL database has not been initialized."
    exit 0
}

& $pgCtl status "--pgdata=$dataDirectory" *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Host "PostgreSQL is already stopped."
    Remove-PostgresProjectDrive
    exit 0
}

& $pgCtl stop "--pgdata=$dataDirectory" "--mode=fast" "--wait"
if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL did not stop cleanly."
}
Write-Host "PostgreSQL stopped."
Remove-PostgresProjectDrive
