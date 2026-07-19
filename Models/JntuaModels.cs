using System;
using System.Collections.Generic;

namespace JntuaResultsFilter.Models
{
    public class Student
    {
        public string HallTicket { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string Course { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ResultSet
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public DateTime? PublishDate { get; set; }
        public string Course { get; set; } = string.Empty;
        public string Regulation { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Semester { get; set; }
        public string ExamType { get; set; } = string.Empty; // Regular / Supplementary
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class StudentResult
    {
        public int Id { get; set; }
        public string HallTicket { get; set; } = string.Empty;
        public string ResultSetId { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public int? InternalMarks { get; set; }
        public int? ExternalMarks { get; set; }
        public int? TotalMarks { get; set; }
        public string Grade { get; set; } = string.Empty;
        public decimal? Credits { get; set; }
        public string ResultStatus { get; set; } = string.Empty; // Pass / Fail / Absent
        public string RawMarksJson { get; set; } = string.Empty;
        public DateTime FetchedAt { get; set; } = DateTime.Now;
    }

    public class ConsolidatedResult
    {
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public string Grade { get; set; } = string.Empty;
        public decimal? Credits { get; set; }
        public string ResultStatus { get; set; } = string.Empty;
        public string ResultSetId { get; set; } = string.Empty;
        public string ResultSetTitle { get; set; } = string.Empty;
        public DateTime? PublishDate { get; set; }
        public int Year { get; set; }
        public int Semester { get; set; }
        public string ExamType { get; set; } = string.Empty;
    }
}
