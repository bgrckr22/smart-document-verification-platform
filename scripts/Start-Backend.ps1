. (Join-Path $PSScriptRoot "LocalDevelopment.Common.ps1")
Import-LocalDevelopmentEnvironment -RequireDatabasePassword

$project = Join-Path $script:ProjectRoot "backend\src\SmartDocumentPlatform.Api\SmartDocumentPlatform.Api.csproj"
& dotnet run --project $project --configuration Release --no-launch-profile --no-restore
