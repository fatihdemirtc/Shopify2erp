#!/usr/bin/env pwsh
# Usage: .\requests\send-webhook.ps1
# Computes HMAC, sends webhook to local API, prints response.

$secret  = "dev-webhook-secret-change-in-production"
$apiUrl  = "http://localhost:5000/api/shopify/order-created"
$payload = Get-Content -Raw "$PSScriptRoot\sample-shopify-order.json"

# Compute HMAC-SHA256
$keyBytes  = [System.Text.Encoding]::UTF8.GetBytes($secret)
$hmac      = New-Object System.Security.Cryptography.HMACSHA256 -ArgumentList @(,$keyBytes)
$bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
$hashBytes = $hmac.ComputeHash($bodyBytes)
$signature = [Convert]::ToBase64String($hashBytes)

Write-Host "Sending webhook to $apiUrl"
Write-Host "X-Shopify-Hmac-Sha256: $signature"

$response = Invoke-WebRequest -Uri $apiUrl `
    -Method POST `
    -ContentType "application/json" `
    -Headers @{ "X-Shopify-Hmac-Sha256" = $signature } `
    -Body $payload `
    -UseBasicParsing

Write-Host "Status: $($response.StatusCode) $($response.StatusDescription)"
