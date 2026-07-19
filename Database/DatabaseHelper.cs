using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using JntuaResultsFilter.Models;

namespace JntuaResultsFilter.Database
{
    public class DatabaseHelper
    {
        private readonly string _connectionString;

        public DatabaseHelper(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Server=(localdb)\\MSSQLLocalDB;Database=JntuaResultsDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";
        }

        // 1. Get Student details
        public Student? GetStudent(string hallTicket)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var query = "SELECT HallTicket, StudentName, Course, Branch, CreatedAt FROM Students WHERE HallTicket = @HallTicket";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@HallTicket", hallTicket);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new Student
                            {
                                HallTicket = reader.GetString(0),
                                StudentName = reader.GetString(1),
                                Course = reader.GetString(2),
                                Branch = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                                CreatedAt = reader.GetDateTime(4)
                            };
                        }
                    }
                }
            }
            return null;
        }

        // 2. Save or update Student details
        public void SaveStudent(Student student)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var query = @"
                    MERGE INTO Students AS target
                    USING (SELECT @HallTicket AS HallTicket) AS source
                    ON (target.HallTicket = source.HallTicket)
                    WHEN MATCHED THEN
                        UPDATE SET StudentName = @StudentName, Course = @Course, Branch = @Branch
                    WHEN NOT MATCHED THEN
                        INSERT (HallTicket, StudentName, Course, Branch)
                        VALUES (@HallTicket, @StudentName, @Course, @Branch);";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@HallTicket", student.HallTicket);
                    cmd.Parameters.AddWithValue("@StudentName", student.StudentName);
                    cmd.Parameters.AddWithValue("@Course", student.Course);
                    cmd.Parameters.AddWithValue("@Branch", string.IsNullOrEmpty(student.Branch) ? (object)DBNull.Value : student.Branch);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 3. Get ResultSet Details
        public ResultSet? GetResultSet(string id)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var query = "SELECT Id, Title, PublishDate, Course, Regulation, Year, Semester, ExamType, CreatedAt FROM ResultSets WHERE Id = @Id";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", id);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new ResultSet
                            {
                                Id = reader.GetString(0),
                                Title = reader.GetString(1),
                                PublishDate = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2),
                                Course = reader.GetString(3),
                                Regulation = reader.GetString(4),
                                Year = reader.GetInt32(5),
                                Semester = reader.GetInt32(6),
                                ExamType = reader.GetString(7),
                                CreatedAt = reader.GetDateTime(8)
                            };
                        }
                    }
                }
            }
            return null;
        }

        // 4. Save ResultSet
        public void SaveResultSet(ResultSet resultSet)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                var query = @"
                    MERGE INTO ResultSets AS target
                    USING (SELECT @Id AS Id) AS source
                    ON (target.Id = source.Id)
                    WHEN MATCHED THEN
                        UPDATE SET Title = @Title, PublishDate = @PublishDate, Course = @Course, Regulation = @Regulation, Year = @Year, Semester = @Semester, ExamType = @ExamType
                    WHEN NOT MATCHED THEN
                        INSERT (Id, Title, PublishDate, Course, Regulation, Year, Semester, ExamType)
                        VALUES (@Id, @Title, @PublishDate, @Course, @Regulation, @Year, @Semester, @ExamType);";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@Id", resultSet.Id);
                    cmd.Parameters.AddWithValue("@Title", resultSet.Title);
                    cmd.Parameters.AddWithValue("@PublishDate", resultSet.PublishDate.HasValue ? (object)resultSet.PublishDate.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@Course", resultSet.Course);
                    cmd.Parameters.AddWithValue("@Regulation", resultSet.Regulation);
                    cmd.Parameters.AddWithValue("@Year", resultSet.Year);
                    cmd.Parameters.AddWithValue("@Semester", resultSet.Semester);
                    cmd.Parameters.AddWithValue("@ExamType", resultSet.ExamType);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // 5. Get student results for a specific result set
        public List<StudentResult> GetStudentResultsForResultSet(string hallTicket, string resultSetId)
        {
            var list = new List<StudentResult>();
            using (var conn = new SqlConnection(_connectionString))
            {
                var query = "SELECT Id, HallTicket, ResultSetId, SubjectCode, SubjectName, InternalMarks, ExternalMarks, TotalMarks, Grade, Credits, ResultStatus, RawMarksJson, FetchedAt FROM StudentResults WHERE HallTicket = @HallTicket AND ResultSetId = @ResultSetId";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@HallTicket", hallTicket);
                    cmd.Parameters.AddWithValue("@ResultSetId", resultSetId);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new StudentResult
                            {
                                Id = reader.GetInt32(0),
                                HallTicket = reader.GetString(1),
                                ResultSetId = reader.GetString(2),
                                SubjectCode = reader.GetString(3),
                                SubjectName = reader.GetString(4),
                                InternalMarks = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                                ExternalMarks = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                                TotalMarks = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7),
                                Grade = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                                Credits = reader.IsDBNull(9) ? (decimal?)null : reader.GetDecimal(9),
                                ResultStatus = reader.GetString(10),
                                RawMarksJson = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
                                FetchedAt = reader.GetDateTime(12)
                            });
                        }
                    }
                }
            }
            return list;
        }

        // 6. Save or update student results in bulk
        public void SaveStudentResults(List<StudentResult> results)
        {
            if (results == null || results.Count == 0) return;

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var transaction = conn.BeginTransaction())
                {
                    try
                    {
                        foreach (var result in results)
                        {
                            var query = @"
                                MERGE INTO StudentResults AS target
                                USING (SELECT @HallTicket AS HallTicket, @ResultSetId AS ResultSetId, @SubjectCode AS SubjectCode) AS source
                                ON (target.HallTicket = source.HallTicket AND target.ResultSetId = source.ResultSetId AND target.SubjectCode = source.SubjectCode)
                                WHEN MATCHED THEN
                                    UPDATE SET SubjectName = @SubjectName, InternalMarks = @InternalMarks, ExternalMarks = @ExternalMarks, 
                                               TotalMarks = @TotalMarks, Grade = @Grade, Credits = @Credits, ResultStatus = @ResultStatus, RawMarksJson = @RawMarksJson, FetchedAt = GETDATE()
                                WHEN NOT MATCHED THEN
                                    INSERT (HallTicket, ResultSetId, SubjectCode, SubjectName, InternalMarks, ExternalMarks, TotalMarks, Grade, Credits, ResultStatus, RawMarksJson)
                                    VALUES (@HallTicket, @ResultSetId, @SubjectCode, @SubjectName, @InternalMarks, @ExternalMarks, @TotalMarks, @Grade, @Credits, @ResultStatus, @RawMarksJson);";

                            using (var cmd = new SqlCommand(query, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@HallTicket", result.HallTicket);
                                cmd.Parameters.AddWithValue("@ResultSetId", result.ResultSetId);
                                cmd.Parameters.AddWithValue("@SubjectCode", result.SubjectCode);
                                cmd.Parameters.AddWithValue("@SubjectName", result.SubjectName);
                                cmd.Parameters.AddWithValue("@InternalMarks", result.InternalMarks.HasValue ? (object)result.InternalMarks.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@ExternalMarks", result.ExternalMarks.HasValue ? (object)result.ExternalMarks.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@TotalMarks", result.TotalMarks.HasValue ? (object)result.TotalMarks.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@Grade", string.IsNullOrEmpty(result.Grade) ? (object)DBNull.Value : result.Grade);
                                cmd.Parameters.AddWithValue("@Credits", result.Credits.HasValue ? (object)result.Credits.Value : DBNull.Value);
                                cmd.Parameters.AddWithValue("@ResultStatus", result.ResultStatus);
                                cmd.Parameters.AddWithValue("@RawMarksJson", string.IsNullOrEmpty(result.RawMarksJson) ? (object)DBNull.Value : result.RawMarksJson);

                                cmd.ExecuteNonQuery();
                            }
                        }
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // 7. Get Consolidated Scorecard (Highest marks or Passed attempt wins per subject)
        public List<ConsolidatedResult> GetConsolidatedResults(string hallTicket)
        {
            var list = new List<ConsolidatedResult>();
            using (var conn = new SqlConnection(_connectionString))
            {
                var query = @"
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
                            sr.ResultSetId,
                            ROW_NUMBER() OVER(
                                PARTITION BY sr.SubjectCode 
                                ORDER BY 
                                    CASE WHEN UPPER(sr.ResultStatus) = 'PASS' OR UPPER(sr.Grade) != 'F' THEN 1 ELSE 2 END,
                                    sr.TotalMarks DESC, 
                                    rs.PublishDate DESC
                            ) as rn
                        FROM StudentResults sr
                        JOIN ResultSets rs ON sr.ResultSetId = rs.Id
                        WHERE sr.HallTicket = @HallTicket
                    )
                    SELECT SubjectCode, SubjectName, Grade, Credits, ResultStatus, ResultSetTitle, PublishDate, Year, Semester, ExamType, ResultSetId
                    FROM RankedResults
                    WHERE rn = 1
                    ORDER BY Year ASC, Semester ASC, SubjectCode ASC;";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@HallTicket", hallTicket);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ConsolidatedResult
                            {
                                SubjectCode = reader.GetString(0),
                                SubjectName = reader.GetString(1),
                                Grade = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Credits = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3),
                                ResultStatus = reader.GetString(4),
                                ResultSetTitle = reader.GetString(5),
                                PublishDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                                Year = reader.GetInt32(7),
                                Semester = reader.GetInt32(8),
                                ExamType = reader.GetString(9),
                                ResultSetId = reader.GetString(10)
                            });
                        }
                    }
                }
            }
            return list;
        }

        // 7b. Get All Attempts/Results of a student (including backlog history)
        public List<ConsolidatedResult> GetAllResults(string hallTicket)
        {
            var list = new List<ConsolidatedResult>();
            using (var conn = new SqlConnection(_connectionString))
            {
                var query = @"
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
                        sr.ResultSetId
                    FROM StudentResults sr
                    JOIN ResultSets rs ON sr.ResultSetId = rs.Id
                    WHERE sr.HallTicket = @HallTicket
                    ORDER BY Year ASC, Semester ASC, SubjectCode ASC, rs.PublishDate DESC;";

                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@HallTicket", hallTicket);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new ConsolidatedResult
                            {
                                SubjectCode = reader.GetString(0),
                                SubjectName = reader.GetString(1),
                                Grade = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                                Credits = reader.IsDBNull(3) ? (decimal?)null : reader.GetDecimal(3),
                                ResultStatus = reader.GetString(4),
                                ResultSetTitle = reader.GetString(5),
                                PublishDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6),
                                Year = reader.GetInt32(7),
                                Semester = reader.GetInt32(8),
                                ExamType = reader.GetString(9),
                                ResultSetId = reader.GetString(10)
                            });
                        }
                    }
                }
            }
            return list;
        }

        // 8. Get IDs of result sets already fetched for a student
        public HashSet<string> GetFetchedResultSetIds(string hallTicket)
        {
            var set = new HashSet<string>();
            using (var conn = new SqlConnection(_connectionString))
            {
                var query = "SELECT DISTINCT ResultSetId FROM StudentResults WHERE HallTicket = @HallTicket";
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@HallTicket", hallTicket);
                    conn.Open();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            set.Add(reader.GetString(0));
                        }
                    }
                }
            }
            return set;
        }
    }
}
