# Disable certificate validation for this test session
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

try {
    # Set Host header manually
    $headers = @{
        "Host" = "jntuaresults.ac.in"
    }
    $resp = Invoke-RestMethod -Method Get -Uri "https://143.244.131.73/app/api/v1/student/resultSets?pageNumber=1&pageSize=5" -Headers $headers -UserAgent "Mozilla/5.0" -TimeoutSec 10
    if ($resp) {
        $resp | ConvertTo-Json -Depth 5
    } else {
        Write-Host "Response is empty"
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        Write-Host "Response Body: $($reader.ReadToEnd())"
    }
}
