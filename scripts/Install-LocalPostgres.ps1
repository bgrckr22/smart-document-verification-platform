$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$toolsDirectory = Join-Path $projectRoot ".tools"
$archivePath = Join-Path $toolsDirectory "postgresql-16.15-windows-x64.zip"
$extractDirectory = Join-Path $toolsDirectory "postgresql-install"
$installationDirectory = Join-Path $toolsDirectory "postgresql"
$downloadUrl = "https://get.enterprisedb.com/postgresql/postgresql-16.15-1-windows-x64-binaries.zip"
$expectedSha256 = "25E6FCDFB8CAEC38691BF461125E7564508760666F7B8E5DC6A5F0818F58F81E"

function Assert-WorkspaceChildPath([string] $Path) {
    $workspacePath = [System.IO.Path]::GetFullPath($projectRoot)
    $candidatePath = [System.IO.Path]::GetFullPath($Path)
    $requiredPrefix = $workspacePath.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar

    if (-not $candidatePath.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside the project: $candidatePath"
    }
}

foreach ($path in @($toolsDirectory, $archivePath, $extractDirectory, $installationDirectory)) {
    Assert-WorkspaceChildPath $path
}

$pgCtl = Join-Path $installationDirectory "bin\pg_ctl.exe"
if (Test-Path -LiteralPath $pgCtl) {
    Write-Host "Project-local PostgreSQL is already installed."
    & (Join-Path $installationDirectory "bin\postgres.exe") --version
    exit 0
}
if (Test-Path -LiteralPath $installationDirectory) {
    throw "The target directory exists but is incomplete: $installationDirectory"
}
if (Test-Path -LiteralPath $extractDirectory) {
    throw "Remove the incomplete extraction directory and try again: $extractDirectory"
}

New-Item -ItemType Directory -Path $toolsDirectory -Force | Out-Null

if (-not (Test-Path -LiteralPath $archivePath)) {
    Write-Host "Downloading PostgreSQL 16.15 into the project..."
    Invoke-WebRequest -Uri $downloadUrl -OutFile $archivePath
}

$actualSha256 = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
if ($actualSha256 -ne $expectedSha256) {
    throw "The PostgreSQL archive checksum did not match the reviewed package."
}

try {
    Expand-Archive -LiteralPath $archivePath -DestinationPath $extractDirectory
    $extractedRoot = Join-Path $extractDirectory "pgsql"
    Assert-WorkspaceChildPath $extractedRoot

    if (-not (Test-Path -LiteralPath (Join-Path $extractedRoot "bin\pg_ctl.exe"))) {
        throw "The extracted PostgreSQL package is incomplete."
    }

    Move-Item -LiteralPath $extractedRoot -Destination $installationDirectory
}
catch {
    if (Test-Path -LiteralPath $extractDirectory) {
        Assert-WorkspaceChildPath $extractDirectory
        Remove-Item -LiteralPath $extractDirectory -Recurse -Force
    }
    throw
}

if (Test-Path -LiteralPath $extractDirectory) {
    Remove-Item -LiteralPath $extractDirectory -Force
}
Remove-Item -LiteralPath $archivePath -Force

Write-Host "PostgreSQL was installed inside $installationDirectory"
& (Join-Path $installationDirectory "bin\postgres.exe") --version
