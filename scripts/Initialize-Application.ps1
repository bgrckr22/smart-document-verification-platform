$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot

function Assert-LastCommandSucceeded {
    param([string] $Step)
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Checking local development tools..."

$dotnet = Get-Command dotnet.exe -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw "Install the .NET 8 SDK (or newer) before using this launcher."
}
$sdkVersions = & $dotnet.Source --list-sdks
Assert-LastCommandSucceeded ".NET SDK check"
$hasSupportedSdk = @($sdkVersions | Where-Object {
    $_ -match '^(\d+)\.' -and [int]$Matches[1] -ge 8
}).Count -gt 0
$runtimes = & $dotnet.Source --list-runtimes
Assert-LastCommandSucceeded ".NET runtime check"
if (-not $hasSupportedSdk -or -not @($runtimes | Where-Object {
    $_ -match '^Microsoft\.AspNetCore\.App 8\.'
}).Count) {
    throw "This project needs a .NET SDK 8+ and the ASP.NET Core 8 runtime."
}

$node = Get-Command node.exe -ErrorAction SilentlyContinue
$npm = Get-Command npm.cmd -ErrorAction SilentlyContinue
if (-not $node -or -not $npm) {
    throw "Install Node.js 22.12+ (or 20.19+) with npm before using this launcher."
}
$nodeVersion = (& $node.Source --version).TrimStart('v')
Assert-LastCommandSucceeded "Node.js version check"
if ($nodeVersion -notmatch '^(\d+)\.(\d+)\.') {
    throw "Could not read the Node.js version: $nodeVersion"
}
$nodeMajor = [int]$Matches[1]
$nodeMinor = [int]$Matches[2]
if (-not (($nodeMajor -eq 20 -and $nodeMinor -ge 19) -or
    ($nodeMajor -eq 22 -and $nodeMinor -ge 12) -or $nodeMajor -ge 23)) {
    throw "Node.js $nodeVersion is unsupported. Install Node.js 22.12+ or 20.19+."
}

$python = Join-Path $projectRoot ".tools\python\python.exe"
if (-not (Test-Path -LiteralPath $python)) {
    $pythonLauncher = Get-Command py.exe -ErrorAction SilentlyContinue
    if ($pythonLauncher) {
        $python = & $pythonLauncher.Source -3.12 -c 'import sys; print(sys.executable)'
    }
    if (-not $python -or -not (Test-Path -LiteralPath $python)) {
        $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue
        if ($pythonCommand) {
            $python = $pythonCommand.Source
        }
    }
}
if (-not $python -or -not (Test-Path -LiteralPath $python)) {
    throw "Install Python 3.12 or place a portable copy in .tools\python before using this launcher."
}
$pythonVersion = & $python -c 'import sys; print(sys.version_info.major * 100 + sys.version_info.minor)'
Assert-LastCommandSucceeded "Python version check"
if ($pythonVersion.Trim() -ne "312") {
    throw "Python 3.12 is required; found version code $pythonVersion."
}

$environmentFile = Join-Path $projectRoot ".env"
if (-not (Test-Path -LiteralPath $environmentFile)) {
    $templatePath = Join-Path $projectRoot ".env.example"
    $template = [System.IO.File]::ReadAllText($templatePath)
    $placeholder = "POSTGRES_PASSWORD=change_this_local_password"
    if (-not $template.Contains($placeholder)) {
        throw "The .env.example template has no password placeholder."
    }

    $bytes = New-Object byte[] 24
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
    }
    finally {
        $generator.Dispose()
    }
    $password = [System.BitConverter]::ToString($bytes).Replace("-", "")
    $content = $template.Replace($placeholder, "POSTGRES_PASSWORD=$password")
    [System.IO.File]::WriteAllText($environmentFile, $content, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "Created a private .env with a random local database password."
}

if (-not (Test-Path -LiteralPath (Join-Path $projectRoot ".tools\postgresql\bin\pg_ctl.exe")) -and
    -not (Get-Command pg_ctl.exe -ErrorAction SilentlyContinue)) {
    & (Join-Path $PSScriptRoot "Install-LocalPostgres.ps1")
}

$venvPython = Join-Path $projectRoot ".venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $venvPython)) {
    Write-Host "Creating the project-local Python environment..."
    & $python -m venv (Join-Path $projectRoot ".venv")
    Assert-LastCommandSucceeded "Python environment creation"
}
& $venvPython -c 'import cv2, fastapi, uvicorn, multipart'
if ($LASTEXITCODE -ne 0) {
    Write-Host "Installing Python dependencies into .venv..."
    & $venvPython -m pip install --no-cache-dir -r (Join-Path $projectRoot "cv-service\requirements-dev.txt")
    Assert-LastCommandSucceeded "Python dependency installation"
}

$assets = Join-Path $projectRoot "backend\src\SmartDocumentPlatform.Api\obj\project.assets.json"
if (-not (Test-Path -LiteralPath $assets)) {
    Write-Host "Restoring .NET dependencies..."
    & $dotnet.Source restore (Join-Path $projectRoot "backend\SmartDocumentPlatform.sln")
    Assert-LastCommandSucceeded ".NET restore"
}

$vite = Join-Path $projectRoot "frontend\node_modules\.bin\vite.cmd"
if (-not (Test-Path -LiteralPath $vite)) {
    Write-Host "Installing frontend dependencies..."
    $env:npm_config_cache = Join-Path $projectRoot ".npm-cache"
    & $npm.Source --prefix (Join-Path $projectRoot "frontend") ci
    Assert-LastCommandSucceeded "Frontend dependency installation"
}

Write-Host "Local setup is ready."
