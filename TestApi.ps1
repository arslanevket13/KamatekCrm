# API Test Script for KamatekCRM
# Usage: Run this script after starting the application (KamatekCrm.exe)

$loginUrl = "http://localhost:5050/api/auth/login"
$customersUrl = "http://localhost:5050/api/customers"

Write-Host "Testing API Connection..." -ForegroundColor Cyan

# 1. Login Request
$loginBody = @{
    Username = "admin"
    Password = "admin123"
} | ConvertTo-Json

try {
    Write-Host "Attempting Login to $loginUrl..."
    $loginResponse = Invoke-RestMethod -Uri $loginUrl -Method Post -Body $loginBody -ContentType "application/json"
    
    if ($loginResponse.success) {
        Write-Host "✔ Login Successful!" -ForegroundColor Green
        Write-Host "  Token Received: $($loginResponse.token.Substring(0, 20))..."
        
        $token = $loginResponse.token
        $headers = @{ Authorization = "Bearer $token" }

        # 2. Get Customers Request (Protected Endpoint)
        try {
            Write-Host "Attempting to fetch Customers..."
            $customers = Invoke-RestMethod -Uri $customersUrl -Method Get -Headers $headers
            Write-Host "✔ Customers Fetched Successfully! Count: $($customers.Count)" -ForegroundColor Green
        }
        catch {
             Write-Host "✘ Failed to fetch customers: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
    else {
        Write-Host "✘ Login Failed: $($loginResponse.message)" -ForegroundColor Red
    }
}
catch {
    Write-Host "✘ Connection Failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Ensure API is running on Port 5050 (http://localhost:5050)" -ForegroundColor Yellow
}

Write-Host "`nTest Complete." -ForegroundColor Cyan
