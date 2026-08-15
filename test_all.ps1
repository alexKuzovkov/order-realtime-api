[CmdletBinding()]
param(
    [switch]$StartServices,
    [string]$BaseUrl = "http://localhost:5001",
    [string]$GatewayUrl = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"
$passed = 0
$failed = 0
$userId = "e2e-user"
$otherUserId = "e2e-other-user"
$clientOrderId = "e2e-$([Guid]::NewGuid().ToString('N'))"

function Pass([string]$Message) {
    $script:passed++
    Write-Host "[PASS] $Message" -ForegroundColor Green
}

function Fail([string]$Message) {
    $script:failed++
    Write-Host "[FAIL] $Message" -ForegroundColor Red
}

function Assert-True([bool]$Condition, [string]$Message) {
    if ($Condition) { Pass $Message } else { Fail $Message }
}

function Invoke-Http {
    param(
        [Parameter(Mandatory)][string]$Method,
        [Parameter(Mandatory)][string]$Uri,
        [hashtable]$Headers = @{},
        [object]$Body = $null
    )

    $params = @{
        Method = $Method
        Uri = $Uri
        Headers = $Headers
        SkipHttpErrorCheck = $true
    }

    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = $Body | ConvertTo-Json -Depth 10 -Compress
    }

    $response = Invoke-WebRequest @params
    $content = $null
    if (-not [string]::IsNullOrWhiteSpace($response.Content)) {
        try { $content = $response.Content | ConvertFrom-Json } catch { $content = $response.Content }
    }

    [pscustomobject]@{
        StatusCode = [int]$response.StatusCode
        Body = $content
        Headers = $response.Headers
    }
}

function Wait-ForHealth([string]$Url) {
    $deadline = (Get-Date).AddSeconds(90)
    do {
        try {
            $response = Invoke-WebRequest -Uri "$Url/health" -SkipHttpErrorCheck -TimeoutSec 3
            if ([int]$response.StatusCode -eq 200) { return $true }
        }
        catch { }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    return $false
}

try {
    if ($StartServices) {
        Write-Host "Starting Docker Compose stack..." -ForegroundColor Cyan
        docker compose down -v --remove-orphans 2>$null
        docker compose up -d --build --wait
        if ($LASTEXITCODE -ne 0) { throw "docker compose up failed with exit code $LASTEXITCODE" }
    }

    Write-Host "[1/9] Health check"
    Assert-True (Wait-ForHealth $BaseUrl) "Direct API instance health endpoint is ready"
    Assert-True (Wait-ForHealth $GatewayUrl) "Nginx gateway health endpoint is ready"

    $headers = @{ "X-User-Id" = $userId }
    $orderPayload = @{
        clientOrderId = $clientOrderId
        symbol = "aapl"
        price = 225.50
        volume = 10
    }

    Write-Host "[2/9] Create order"
    $create = Invoke-Http -Method POST -Uri "$BaseUrl/api/orders" -Headers $headers -Body $orderPayload
    Assert-True ($create.StatusCode -eq 201) "New order returns HTTP 201"
    Assert-True ($create.Body.symbol -eq "AAPL") "Order symbol is normalized"
    Assert-True ($create.Body.state -eq "active") "New order starts in Active state"
    $orderId = $create.Body.id

    Write-Host "[3/9] Read order by ID"
    $get = Invoke-Http -Method GET -Uri "$BaseUrl/api/orders/$orderId" -Headers $headers
    Assert-True ($get.StatusCode -eq 200) "Owner can read order by ID"
    Assert-True ($get.Body.state -eq "active") "Order-by-ID endpoint exposes Active state"
    $otherGet = Invoke-Http -Method GET -Uri "$BaseUrl/api/orders/$orderId" -Headers @{ "X-User-Id" = $otherUserId }
    Assert-True ($otherGet.StatusCode -eq 404) "Other user cannot read the order"

    Write-Host "[4/9] Idempotent retry"
    $retry = Invoke-Http -Method POST -Uri "$BaseUrl/api/orders" -Headers $headers -Body $orderPayload
    Assert-True ($retry.StatusCode -eq 200) "Idempotent retry returns HTTP 200"
    Assert-True ($retry.Body.id -eq $orderId) "Idempotent retry returns the same order ID"

    Write-Host "[5/9] Idempotency conflict"
    $conflictPayload = $orderPayload.Clone()
    $conflictPayload.price = 226.00
    $conflict = Invoke-Http -Method POST -Uri "$BaseUrl/api/orders" -Headers $headers -Body $conflictPayload
    Assert-True ($conflict.StatusCode -eq 409) "Reusing idempotency key with another payload returns HTTP 409"

    Write-Host "[6/9] Validation"
    $invalidPayload = @{
        clientOrderId = "bad-order-0001"
        symbol = "AA PL"
        price = 0
        volume = 0
    }
    $invalid = Invoke-Http -Method POST -Uri "$BaseUrl/api/orders" -Headers $headers -Body $invalidPayload
    Assert-True ($invalid.StatusCode -eq 400) "Invalid request returns HTTP 400"

    Write-Host "[7/9] Business rules"
    $limitPayload = @{
        clientOrderId = "limit-$([Guid]::NewGuid().ToString('N'))"
        symbol = "AAPL"
        price = 1
        volume = 100001
    }
    $limit = Invoke-Http -Method POST -Uri "$BaseUrl/api/orders" -Headers $headers -Body $limitPayload
    Assert-True ($limit.StatusCode -eq 422) "Business-rule violation returns HTTP 422"

    Write-Host "[8/9] Active-order list and user isolation"
    $active = Invoke-Http -Method GET -Uri "$BaseUrl/api/orders/active" -Headers $headers
    Assert-True ($active.StatusCode -eq 200) "Active-order endpoint returns HTTP 200"
    Assert-True (@($active.Body | Where-Object { $_.id -eq $orderId }).Count -eq 1) "Active order is visible to its owner"
    $otherActive = Invoke-Http -Method GET -Uri "$BaseUrl/api/orders/active" -Headers @{ "X-User-Id" = $otherUserId }
    Assert-True (@($otherActive.Body | Where-Object { $_.id -eq $orderId }).Count -eq 0) "Active order is hidden from other users"

    Write-Host "[9/9] Expiration state transition"
    $deadline = (Get-Date).AddSeconds(30)
    $expired = $null
    do {
        Start-Sleep -Seconds 2
        $expired = Invoke-Http -Method GET -Uri "$BaseUrl/api/orders/$orderId" -Headers $headers
    } while ($expired.Body.state -eq "active" -and (Get-Date) -lt $deadline)

    Assert-True ($expired.Body.state -eq "inactive") "Expired order transitions to Inactive state"
    $activeAfterExpiration = Invoke-Http -Method GET -Uri "$BaseUrl/api/orders/active" -Headers $headers
    Assert-True (@($activeAfterExpiration.Body | Where-Object { $_.id -eq $orderId }).Count -eq 0) "Inactive order is excluded from active-order list"
}
finally {
    Write-Host "========================================"
    Write-Host " Test summary"
    Write-Host "========================================"
    Write-Host "Passed: $passed"
    Write-Host "Failed: $failed"

    if ($failed -gt 0) {
        exit 1
    }

    Write-Host "All end-to-end checks passed." -ForegroundColor Green
}
