$joinYear = 2022
$completionYear = 2026
$targetYear = "1"
$examType = "Regular" # Or "Supplementary"

# Fetch from real API
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
$headers = @{ "Host" = "jntuaresults.ac.in" }
$resp = Invoke-RestMethod -Method Get -Uri "https://143.244.131.73/app/api/v1/student/resultSets?searchString=B.Tech&pageNumber=1&pageSize=200" -Headers $headers -UserAgent "Mozilla/5.0"

if (-not $resp -or -not $resp.data -or -not $resp.data.responses) {
    Write-Host "Error: Could not retrieve result sets from API"
    exit
}

Write-Host "Total result sets: $($resp.data.responses.Count)"
$matchedCount = 0

foreach ($dto in $resp.data.responses) {
    $title = $dto.title
    
    # 1. Course Match
    $courseMatch = $title -like "*B.Tech*"
    
    # 2. Regulation Match (R20)
    $regMatch = $title -match "\((R\d+)\)"
    $parsedReg = ""
    if ($regMatch) { $parsedReg = $Matches[1] }
    $isR20 = $parsedReg -eq "R20"

    # 3. Publish Year
    $pubDate = [DateTime]::Parse($dto.publishDate)
    $pubYear = $pubDate.Year
    $joinYearMatch = ($pubYear -ge $joinYear) -and ($pubYear -le $completionYear)

    # 4. Target Year (1st Year)
    $parsedYear = 0
    if ($title -like "*IV Year*" -or $title -like "*4 Year*") { $parsedYear = 4 }
    elseif ($title -like "*III Year*" -or $title -like "*3 Year*") { $parsedYear = 3 }
    elseif ($title -like "*II Year*" -or $title -like "*2 Year*") { $parsedYear = 2 }
    elseif ($title -like "*I Year*" -or $title -like "*1 Year*") { $parsedYear = 1 }
    $targetYearMatch = $parsedYear -eq [int]$targetYear

    # 5. Exam Type Match
    $examTypeMatch = $false
    if ($examType -eq "Regular") {
        $examTypeMatch = $title -like "*Regular*"
    } elseif ($examType -eq "Supplementary") {
        $examTypeMatch = ($title -like "*Supplementary*") -or ($title -like "*Suppl*")
    }

    # If it is I Year and R20, print status
    if ($courseMatch -and $isR20 -and $targetYearMatch) {
        $finalMatch = $joinYearMatch -and $examTypeMatch
        Write-Host "Title: $title"
        Write-Host "  PubYear: $pubYear (JoinMatch: $joinYearMatch)"
        Write-Host "  TypeMatch: $examTypeMatch (Target: $examType)"
        Write-Host "  Match: $finalMatch"
        if ($finalMatch) { $matchedCount++ }
        Write-Host "----------------"
    }
}

Write-Host "Total Matched: $matchedCount"
