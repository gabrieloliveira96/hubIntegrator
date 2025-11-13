# Script de teste SEM autenticação (para desenvolvimento rápido)

Write-Host "=== Teste Rápido - Sem Autenticação ===" -ForegroundColor Green
Write-Host ""

$inboundUrl = "http://localhost:5001"
$idempotencyKey = [System.Guid]::NewGuid().ToString()
$nonce = [System.Guid]::NewGuid().ToString()
$timestamp = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()

$payload = @{
    partnerCode = "PARTNER01"
    type = "ORDER"
    payload = '{"orderId":"12345","customerId":"CUST001"}'
} | ConvertTo-Json

$headers = @{
    "Content-Type" = "application/json"
    "Idempotency-Key" = $idempotencyKey
    "X-Nonce" = $nonce
    "X-Timestamp" = $timestamp.ToString()
}

Write-Host "Enviando requisição..." -ForegroundColor Cyan
Write-Host "URL: $inboundUrl/requests" -ForegroundColor Gray
Write-Host ""

try {
    $response = Invoke-WebRequest -Uri "$inboundUrl/requests" `
        -Method Post `
        -Headers $headers `
        -Body $payload `
        -ErrorAction Stop
    
    Write-Host "Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Resposta:" -ForegroundColor Yellow
    $response.Content | ConvertFrom-Json | ConvertTo-Json -Depth 5 | Write-Host -ForegroundColor White
    
} catch {
    Write-Host "Erro: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "Detalhes: $responseBody" -ForegroundColor Red
    }
}

