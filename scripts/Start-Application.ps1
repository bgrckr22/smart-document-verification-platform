param([switch] $NoBrowser)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot

function Test-Endpoint {
    param(
        [string] $Url,
        [string] $ExpectedText
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
        return $response.StatusCode -eq 200 -and $response.Content -match $ExpectedText
    }
    catch {
        return $false
    }
}

function Wait-ForEndpoint {
    param(
        [string] $Name,
        [string] $Url,
        [string] $ExpectedText,
        [int] $TimeoutSeconds = 120
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-Endpoint -Url $Url -ExpectedText $ExpectedText) {
            Write-Host "$Name is ready: $Url"
            return
        }
        Start-Sleep -Milliseconds 700
    }

    throw "$Name did not become ready at $Url. Check its service window for errors."
}

function Start-ServiceWindow {
    param(
        [string] $Name,
        [string] $ScriptName,
        [int] $Port,
        [string] $Url,
        [string] $ExpectedText
    )

    if (Test-Endpoint -Url $Url -ExpectedText $ExpectedText) {
        Write-Host "$Name is already running."
        return
    }

    $listener = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if ($listener) {
        throw "Port $Port is in use, but $Name is not responding as expected at $Url. Close the conflicting application first."
    }

    $scriptPath = Join-Path $PSScriptRoot $ScriptName
    Write-Host "Starting $Name in a separate window..."
    Start-Process -FilePath "powershell.exe" `
        -ArgumentList "-NoProfile -ExecutionPolicy Bypass -NoExit -File `"$scriptPath`"" `
        -WorkingDirectory $projectRoot -WindowStyle Normal | Out-Null

    Wait-ForEndpoint -Name $Name -Url $Url -ExpectedText $ExpectedText
}

Write-Host "Starting Smart Document Verification Platform..."
& (Join-Path $PSScriptRoot "Initialize-Application.ps1")
& (Join-Path $PSScriptRoot "Start-LocalPostgres.ps1")

Start-ServiceWindow -Name "FastAPI/OpenCV" -ScriptName "Start-CvService.ps1" `
    -Port 8000 -Url "http://127.0.0.1:8000/health" `
    -ExpectedText '"service"\s*:\s*"cv-service"'

Start-ServiceWindow -Name "ASP.NET Core API" -ScriptName "Start-Backend.ps1" `
    -Port 5000 -Url "http://127.0.0.1:5000/health/ready" `
    -ExpectedText '"status"\s*:\s*"ready"'

Start-ServiceWindow -Name "React frontend" -ScriptName "Start-Frontend.ps1" `
    -Port 5173 -Url "http://127.0.0.1:5173/" `
    -ExpectedText '<title>Smart Document Verification</title>'

Write-Host "All services are ready. Dashboard: http://localhost:5173/"
if (-not $NoBrowser) {
    Start-Process "http://localhost:5173/"
}
