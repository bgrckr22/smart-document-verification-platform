. (Join-Path $PSScriptRoot "LocalDevelopment.Common.ps1")
Import-LocalDevelopmentEnvironment

Push-Location (Join-Path $script:ProjectRoot "frontend")
try {
    & npm.cmd run dev
}
finally {
    Pop-Location
}
