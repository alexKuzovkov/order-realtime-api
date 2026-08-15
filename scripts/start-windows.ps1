[CmdletBinding()]
param(
    [switch]$SkipTests,
    [string]$Url = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src/OrderRealtime.Api/OrderRealtime.Api.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK was not found. Install the .NET 9 SDK: https://dotnet.microsoft.com/download/dotnet/9.0"
}

$sdkVersion = & dotnet --version
if ([version]($sdkVersion.Split('-')[0]) -lt [version]"9.0.0") {
    throw ".NET SDK 9 or newer is required. Detected version: $sdkVersion"
}

Push-Location $repositoryRoot
try {
    Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
    & dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE" }

    if (-not $SkipTests) {
        Write-Host "Running tests..." -ForegroundColor Cyan
        & dotnet test --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Tests failed with exit code $LASTEXITCODE" }
    }

    Write-Host "Starting API at $Url" -ForegroundColor Green
    Write-Host "UI: $Url | Swagger: $Url/swagger | Health: $Url/health"
    Write-Host "Press Ctrl+C to stop."
    & dotnet run --project $projectPath --no-restore --urls $Url
    if ($LASTEXITCODE -ne 0) { throw "Application exited with code $LASTEXITCODE" }
}
finally {
    Pop-Location
}
