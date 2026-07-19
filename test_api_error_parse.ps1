$body = @{
    resultTitleId = "ee090aa9-02bc-49d8-8bc4-7e89346e5ffb"
    hallTicketNumber = "22L21A0578"
    verificationToken = "invalid_token"
} | ConvertTo-Json

try {
    $resp = Invoke-RestMethod -Method Post -Uri "https://jntuaresults.ac.in/app/api/v1/student/searchResult" -Body $body -ContentType "application/json" -UserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36" -TimeoutSec 10
    $resp | ConvertTo-Json -Depth 5
} catch {
    Write-Host "Exception Message: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        Write-Host "Status Code: $($_.Exception.Response.StatusCode)"
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $errorBody = $reader.ReadToEnd()
        Write-Host "Response Body: $errorBody"
    }
}
