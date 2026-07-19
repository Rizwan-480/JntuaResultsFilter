-- Database creation script for JNTUA Results Aggregator
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'JntuaResultsDb')
BEGIN
    CREATE DATABASE JntuaResultsDb;
END
GO

USE JntuaResultsDb;
GO

-- 1. Students Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Students')
BEGIN
    CREATE TABLE Students (
        HallTicket VARCHAR(50) NOT NULL PRIMARY KEY,
        StudentName NVARCHAR(200) NOT NULL,
        Course VARCHAR(50) NOT NULL,
        Branch NVARCHAR(100) NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- 2. ResultSets Table (Stores JNTUA exam title metadata)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ResultSets')
BEGIN
    CREATE TABLE ResultSets (
        Id VARCHAR(100) NOT NULL PRIMARY KEY, -- JNTUA's resultTitleId (UUID string)
        Title NVARCHAR(500) NOT NULL,
        PublishDate DATETIME NULL,
        Course VARCHAR(50) NOT NULL,
        Regulation VARCHAR(50) NOT NULL,
        Year INT NOT NULL,
        Semester INT NOT NULL,
        ExamType VARCHAR(50) NOT NULL, -- Regular / Supplementary
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
END
GO

-- 3. StudentResults Table (Stores individual subject marks)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StudentResults')
BEGIN
    CREATE TABLE StudentResults (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        HallTicket VARCHAR(50) NOT NULL FOREIGN KEY REFERENCES Students(HallTicket) ON DELETE CASCADE,
        ResultSetId VARCHAR(100) NOT NULL FOREIGN KEY REFERENCES ResultSets(Id) ON DELETE CASCADE,
        SubjectCode VARCHAR(50) NOT NULL,
        SubjectName NVARCHAR(200) NOT NULL,
        InternalMarks INT NULL,
        ExternalMarks INT NULL,
        TotalMarks INT NULL,
        Grade VARCHAR(10) NULL,
        Credits DECIMAL(4,2) NULL,
        ResultStatus VARCHAR(20) NOT NULL, -- Pass / Fail / Absent / Malpractice
        RawMarksJson NVARCHAR(MAX) NULL, -- Stores original marksObject for reference
        FetchedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT UC_Student_ResultSet_Subject UNIQUE (HallTicket, ResultSetId, SubjectCode)
    );
END
GO
