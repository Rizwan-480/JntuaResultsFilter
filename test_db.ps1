# Integration test for JNTUA Results Aggregator Database

$connStr = "Server=(localdb)\MSSQLLocalDB;Database=JntuaResultsDb;Trusted_Connection=True;TrustServerCertificate=True"
$connection = New-Object System.Data.SqlClient.SqlConnection($connStr)

try {
    $connection.Open()
    Write-Host "Connected to JntuaResultsDb successfully."

    # 1. Clean up existing dummy data
    $cleanupCmd = New-Object System.Data.SqlClient.SqlCommand("DELETE FROM StudentResults WHERE HallTicket = '20001A0501'; DELETE FROM ResultSets WHERE Id IN ('R-Regular-1-1', 'R-Supply-1-1'); DELETE FROM Students WHERE HallTicket = '20001A0501';", $connection)
    $cleanupCmd.ExecuteNonQuery() > $null
    Write-Host "Database cleaned up for testing."

    # 2. Insert dummy student
    $studentCmd = New-Object System.Data.SqlClient.SqlCommand("INSERT INTO Students (HallTicket, StudentName, Course, Branch) VALUES ('20001A0501', 'Test Student Name', 'B.Tech', 'CSE (R20)');", $connection)
    $studentCmd.ExecuteNonQuery() > $null
    Write-Host "Inserted mock student: 20001A0501"

    # 3. Insert mock ResultSets (Regular & Supplementary)
    $rsCmd1 = New-Object System.Data.SqlClient.SqlCommand("INSERT INTO ResultSets (Id, Title, PublishDate, Course, Regulation, Year, Semester, ExamType) VALUES ('R-Regular-1-1', 'B.Tech I Year I Semester (R20) Regular Exams, July 2021', '2021-07-15', 'B.Tech', 'R20', 1, 1, 'Regular');", $connection)
    $rsCmd1.ExecuteNonQuery() > $null
    $rsCmd2 = New-Object System.Data.SqlClient.SqlCommand("INSERT INTO ResultSets (Id, Title, PublishDate, Course, Regulation, Year, Semester, ExamType) VALUES ('R-Supply-1-1', 'B.Tech I Year I Semester (R20) Supplementary Exams, Dec 2021', '2021-12-20', 'B.Tech', 'R20', 1, 1, 'Supplementary');", $connection)
    $rsCmd2.ExecuteNonQuery() > $null
    Write-Host "Inserted mock ResultSets: Regular & Supplementary"

    # 4. Insert mock Results (Subject 1: failed first, passed later. Subject 2: passed first)
    # Subject 1: Regular attempt (Fail)
    $resCmd1 = New-Object System.Data.SqlClient.SqlCommand("INSERT INTO StudentResults (HallTicket, ResultSetId, SubjectCode, SubjectName, InternalMarks, ExternalMarks, TotalMarks, Grade, Credits, ResultStatus) VALUES ('20001A0501', 'R-Regular-1-1', '20A05101', 'Programming in C', 10, 15, 25, 'F', 0.0, 'Fail');", $connection)
    $resCmd1.ExecuteNonQuery() > $null

    # Subject 1: Supplementary attempt (Pass)
    $resCmd2 = New-Object System.Data.SqlClient.SqlCommand("INSERT INTO StudentResults (HallTicket, ResultSetId, SubjectCode, SubjectName, InternalMarks, ExternalMarks, TotalMarks, Grade, Credits, ResultStatus) VALUES ('20001A0501', 'R-Supply-1-1', '20A05101', 'Programming in C', 10, 50, 60, 'B', 3.0, 'Pass');", $connection)
    $resCmd2.ExecuteNonQuery() > $null

    # Subject 2: Regular attempt (Pass)
    $resCmd3 = New-Object System.Data.SqlClient.SqlCommand("INSERT INTO StudentResults (HallTicket, ResultSetId, SubjectCode, SubjectName, InternalMarks, ExternalMarks, TotalMarks, Grade, Credits, ResultStatus) VALUES ('20001A0501', 'R-Regular-1-1', '20A05102', 'Engineering Mathematics-I', 25, 45, 70, 'A', 4.0, 'Pass');", $connection)
    $resCmd3.ExecuteNonQuery() > $null
    Write-Host "Inserted mock subject results."

    # 5. Run the consolidated rank query
    Write-Host "`nRunning Consolidated Scorecard Query..."
    $query = @"
        WITH RankedResults AS (
            SELECT 
                sr.SubjectCode,
                sr.SubjectName,
                sr.Grade,
                sr.Credits,
                sr.ResultStatus,
                rs.Title AS ResultSetTitle,
                rs.PublishDate,
                rs.Year,
                rs.Semester,
                rs.ExamType,
                ROW_NUMBER() OVER(
                    PARTITION BY sr.SubjectCode 
                    ORDER BY 
                        CASE WHEN UPPER(sr.ResultStatus) = 'PASS' OR UPPER(sr.Grade) != 'F' THEN 1 ELSE 2 END,
                        sr.TotalMarks DESC, 
                        rs.PublishDate DESC
                ) as rn
            FROM StudentResults sr
            JOIN ResultSets rs ON sr.ResultSetId = rs.Id
            WHERE sr.HallTicket = '20001A0501'
        )
        SELECT SubjectCode, SubjectName, Grade, Credits, ResultStatus, ResultSetTitle, ExamType
        FROM RankedResults
        WHERE rn = 1
        ORDER BY SubjectCode ASC;
"@
    $searchCmd = New-Object System.Data.SqlClient.SqlCommand($query, $connection)
    $reader = $searchCmd.ExecuteReader()
    
    Write-Host "------------------------------------------------------------------------------------------------"
    Write-Host "SubjectCode | SubjectName               | Grade | Credits | Status | Source Exam Sheet"
    Write-Host "------------------------------------------------------------------------------------------------"
    while ($reader.Read()) {
        $code = $reader.GetString(0).PadRight(11)
        $name = $reader.GetString(1).PadRight(25)
        $grade = $reader.GetString(2).PadRight(5)
        $credits = $reader.GetDecimal(3).ToString("0.0").PadRight(7)
        $status = $reader.GetString(4).PadRight(6)
        $exam = $reader.GetString(5)
        Write-Host "$code | $name | $grade | $credits | $status | $exam"
    }
    Write-Host "------------------------------------------------------------------------------------------------"
    $reader.Close()

} catch {
    Write-Host "Error occurred: $($_.Exception.Message)"
} finally {
    $connection.Close()
    Write-Host "Connection closed."
}
