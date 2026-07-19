try {
    $resp = Invoke-RestMethod -Uri "https://jntuaresults.ac.in/app/api/v1/student/resultSets?searchString=B.Tech%20I%20Year%20I%20Semester%20(R20)&pageNumber=1&pageSize=20" -UserAgent "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36" -TimeoutSec 10
    if ($resp) {
        $resp | ConvertTo-Json -Depth 5
    } else {
        Write-Host "Response is empty"
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)"
}
