$connectionString = "Server=(localdb)\MSSQLLocalDB;Database=JntuaResultsDb;Trusted_Connection=True;Encrypt=False;"
$connection = New-Object System.Data.SqlClient.SqlConnection($connectionString)

try {
    $connection.Open()
    
    # 1. Check Student
    $cmd = $connection.CreateCommand()
    $cmd.CommandText = "SELECT * FROM Students WHERE HallTicket = '22L21A0578'"
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter($cmd)
    $dt = New-Object System.Data.DataTable
    $adapter.Fill($dt)
    Write-Host "=== Students ==="
    $dt | Format-Table -AutoSize
    
    # 2. Check Fetched Result Sets count
    $cmd.CommandText = "SELECT COUNT(*) FROM StudentResults WHERE HallTicket = '22L21A0578'"
    $count = $cmd.ExecuteScalar()
    Write-Host "=== Total Stored Result Rows for 22L21A0578: $count ==="

    # 3. Check distinct Result Sets fetched
    $cmd.CommandText = "SELECT DISTINCT r.Id, r.Title, r.ExamType FROM ResultSets r JOIN StudentResults sr ON r.Id = sr.ResultSetId WHERE sr.HallTicket = '22L21A0578'"
    $dt2 = New-Object System.Data.DataTable
    $adapter.SelectCommand = $cmd
    $adapter.Fill($dt2)
    Write-Host "=== Fetched Result Sets in DB ==="
    $dt2 | Format-Table -AutoSize

    # 4. Check all distinct ResultSets in the DB
    $cmd.CommandText = "SELECT Id, Title, ExamType, PublishDate FROM ResultSets"
    $dt3 = New-Object System.Data.DataTable
    $adapter.SelectCommand = $cmd
    $adapter.Fill($dt3)
    Write-Host "=== All Result Sets in DB ==="
    $dt3 | Format-Table -AutoSize

} catch {
    Write-Host "Error: $($_.Exception.Message)"
} finally {
    $connection.Close()
}
