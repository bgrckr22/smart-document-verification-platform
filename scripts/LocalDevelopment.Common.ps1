$ErrorActionPreference = "Stop"

$script:ProjectRoot = Split-Path -Parent $PSScriptRoot

function Import-LocalDevelopmentEnvironment {
    param([switch] $RequireDatabasePassword)

    $environmentFile = Join-Path $script:ProjectRoot ".env"
    if (-not (Test-Path -LiteralPath $environmentFile)) {
        throw "Missing .env. Copy .env.example to .env and replace POSTGRES_PASSWORD."
    }

    foreach ($line in Get-Content -LiteralPath $environmentFile) {
        $trimmedLine = $line.Trim()
        if (-not $trimmedLine -or $trimmedLine.StartsWith("#")) {
            continue
        }

        $separatorIndex = $trimmedLine.IndexOf("=")
        if ($separatorIndex -le 0) {
            throw "Invalid .env entry: $trimmedLine"
        }

        $name = $trimmedLine.Substring(0, $separatorIndex).Trim()
        $value = $trimmedLine.Substring($separatorIndex + 1).Trim().Trim('"').Trim("'")
        [Environment]::SetEnvironmentVariable($name, $value, "Process")
    }

    $defaults = @{
        POSTGRES_DB = "smart_document_db"
        POSTGRES_USER = "smartdocs"
        POSTGRES_PORT = "5432"
        LOCAL_PROJECT_DRIVE = "S:"
        LOCAL_BACKEND_URL = "http://localhost:5000"
        LOCAL_CV_URL = "http://localhost:8000"
        VITE_API_BASE_URL = "http://localhost:5000"
        MAX_UPLOAD_BYTES = "10485760"
        MAX_IMAGE_PIXELS = "40000000"
    }
    foreach ($entry in $defaults.GetEnumerator()) {
        if ([string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($entry.Key))) {
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, "Process")
        }
    }

    if ($env:POSTGRES_DB -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
        throw "POSTGRES_DB must contain only letters, numbers, and underscores."
    }
    if ($env:POSTGRES_USER -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
        throw "POSTGRES_USER must contain only letters, numbers, and underscores."
    }
    if ($env:POSTGRES_PORT -notmatch '^\d{1,5}$' -or
        [int]$env:POSTGRES_PORT -lt 1 -or [int]$env:POSTGRES_PORT -gt 65535) {
        throw "POSTGRES_PORT must be a valid TCP port."
    }

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:ASPNETCORE_URLS = $env:LOCAL_BACKEND_URL
    $env:ComputerVision__BaseUrl = $env:LOCAL_CV_URL
    $env:Uploads__MaxBytes = $env:MAX_UPLOAD_BYTES

    if ($RequireDatabasePassword) {
        if ([string]::IsNullOrWhiteSpace($env:POSTGRES_PASSWORD) -or
            $env:POSTGRES_PASSWORD -eq "change_this_local_password") {
            throw "Set a private POSTGRES_PASSWORD in .env before starting the database or backend."
        }

        $escapedPassword = $env:POSTGRES_PASSWORD.Replace('"', '""')
        $env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=$($env:POSTGRES_PORT);Database=$($env:POSTGRES_DB);Username=$($env:POSTGRES_USER);Password=`"$escapedPassword`""
    }
}

function Get-PostgresBinDirectory {
    param([string] $ProjectPath = $script:ProjectRoot)

    $projectLocalBin = Join-Path $ProjectPath ".tools\postgresql\bin"
    if (Test-Path -LiteralPath (Join-Path $projectLocalBin "pg_ctl.exe")) {
        return $projectLocalBin
    }

    $pgCtl = Get-Command "pg_ctl.exe" -ErrorAction SilentlyContinue
    if ($pgCtl) {
        return Split-Path -Parent $pgCtl.Source
    }

    throw "PostgreSQL was not found. Install PostgreSQL 16 or place its binaries in .tools\postgresql."
}

function Get-PostgresProjectPath {
    if ($script:ProjectRoot -cmatch '^[\x00-\x7F]+$') {
        return $script:ProjectRoot
    }

    if ($env:LOCAL_PROJECT_DRIVE -notmatch '^[D-Zd-z]:$') {
        throw "LOCAL_PROJECT_DRIVE must be an unused drive letter from D: through Z:."
    }

    $driveName = $env:LOCAL_PROJECT_DRIVE.Substring(0, 1).ToUpperInvariant()
    $markerDirectory = Join-Path $script:ProjectRoot ".local-data"
    $markerPath = Join-Path $markerDirectory "project-root.marker"
    New-Item -ItemType Directory -Path $markerDirectory -Force | Out-Null
    if (-not (Test-Path -LiteralPath $markerPath)) {
        [System.IO.File]::WriteAllText($markerPath, [guid]::NewGuid().ToString("N"))
    }
    $expectedMarker = [System.IO.File]::ReadAllText($markerPath)
    $mappedMarkerPath = "$($env:LOCAL_PROJECT_DRIVE)\.local-data\project-root.marker"

    if (Get-PSDrive -Name $driveName -ErrorAction SilentlyContinue) {
        if ((Test-Path -LiteralPath $mappedMarkerPath) -and
            [System.IO.File]::ReadAllText($mappedMarkerPath) -eq $expectedMarker) {
            return "$($env:LOCAL_PROJECT_DRIVE)\"
        }

        throw "Drive $($env:LOCAL_PROJECT_DRIVE) is already in use. Choose another LOCAL_PROJECT_DRIVE in .env."
    }

    & subst.exe $env:LOCAL_PROJECT_DRIVE $script:ProjectRoot
    if ($LASTEXITCODE -ne 0) {
        throw "Could not map $($env:LOCAL_PROJECT_DRIVE) to the project directory."
    }
    if (-not (Test-Path -LiteralPath $mappedMarkerPath) -or
        [System.IO.File]::ReadAllText($mappedMarkerPath) -ne $expectedMarker) {
        throw "The temporary project drive does not point to this repository."
    }

    return "$($env:LOCAL_PROJECT_DRIVE)\"
}

function Remove-PostgresProjectDrive {
    if ($script:ProjectRoot -cmatch '^[\x00-\x7F]+$') {
        return
    }

    $markerPath = Join-Path $script:ProjectRoot ".local-data\project-root.marker"
    $mappedMarkerPath = "$($env:LOCAL_PROJECT_DRIVE)\.local-data\project-root.marker"
    if (-not (Test-Path -LiteralPath $markerPath) -or
        -not (Test-Path -LiteralPath $mappedMarkerPath) -or
        [System.IO.File]::ReadAllText($markerPath) -ne [System.IO.File]::ReadAllText($mappedMarkerPath)) {
        throw "Refusing to remove a project drive alias that is not owned by this repository."
    }

    & subst.exe $env:LOCAL_PROJECT_DRIVE /d
    if ($LASTEXITCODE -ne 0) {
        throw "Could not remove temporary drive $($env:LOCAL_PROJECT_DRIVE)."
    }
}
