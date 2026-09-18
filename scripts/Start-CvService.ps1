. (Join-Path $PSScriptRoot "LocalDevelopment.Common.ps1")
Import-LocalDevelopmentEnvironment

$python = Join-Path $script:ProjectRoot ".venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $python)) {
    $pythonCommand = Get-Command "python.exe" -ErrorAction SilentlyContinue
    if (-not $pythonCommand) {
        throw "Python was not found. Create .venv and install cv-service\requirements-dev.txt."
    }
    $python = $pythonCommand.Source
}

$uvicornArguments = @(
    "-m", "uvicorn",
    "app.main:app",
    "--host", "127.0.0.1",
    "--port", "8000",
    "--reload",
    "--reload-dir", "."
)

Push-Location (Join-Path $script:ProjectRoot "cv-service")
try {
    & $python @uvicornArguments
}
finally {
    Pop-Location
}
