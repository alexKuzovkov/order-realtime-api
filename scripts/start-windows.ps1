[CmdletBinding()]
param(
    [switch]$SkipTests,
    [string]$Url = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot "src/OrderRealtime.Api/OrderRealtime.Api.csproj"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw ".NET SDK не найден. Установите .NET 9 SDK: https://dotnet.microsoft.com/download/dotnet/9.0"
}

$sdkVersion = & dotnet --version
if ([version]($sdkVersion.Split('-')[0]) -lt [version]"9.0.0") {
    throw "Требуется .NET SDK 9 или новее. Обнаружена версия: $sdkVersion"
}

Push-Location $repositoryRoot
try {
    Write-Host "Restoring NuGet packages..." -ForegroundColor Cyan
    & dotnet restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet restore завершился с кодом $LASTEXITCODE" }

    if (-not $SkipTests) {
        Write-Host "Running tests..." -ForegroundColor Cyan
        & dotnet test --configuration Release --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Тесты завершились с кодом $LASTEXITCODE" }
    }

    Write-Host "Starting API at $Url" -ForegroundColor Green
    Write-Host "UI: $Url | Swagger: $Url/swagger | Health: $Url/health"
    Write-Host "Press Ctrl+C to stop."
    & dotnet run --project $projectPath --no-restore --urls $Url
    if ($LASTEXITCODE -ne 0) { throw "Приложение завершилось с кодом $LASTEXITCODE" }
}
finally {
    Pop-Location
}
