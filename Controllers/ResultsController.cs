using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using JntuaResultsFilter.Database;
using JntuaResultsFilter.Models;
using JntuaResultsFilter.Services;

namespace JntuaResultsFilter.Controllers
{
    public class ResultsController : Controller
    {
        private readonly DatabaseHelper _dbHelper;
        private readonly JntuaApiService _apiService;

        public ResultsController(DatabaseHelper dbHelper, JntuaApiService apiService)
        {
            _dbHelper = dbHelper;
            _apiService = apiService;
        }

        // GET: /Results
        public async Task<IActionResult> Index()
        {
            var logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
            if (!System.IO.File.Exists(logoPath))
            {
                try
                {
                    var logoBytes = await _apiService.DownloadLogoAsync();
                    if (logoBytes != null)
                    {
                        var dir = Path.GetDirectoryName(logoPath);
                        if (dir != null && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        await System.IO.File.WriteAllBytesAsync(logoPath, logoBytes);
                    }
                }
                catch
                {
                    // Ignore so that it doesn't block site if JNTUA server is down
                }
            }
            return View();
        }

        // POST: /Results/Search
        [HttpPost]
        public IActionResult Search(string course, string branch, string regulation, int joinYear, int completionYear, string targetYear, string examType, string showStatus, string hallTicketNumber)
        {
            if (string.IsNullOrWhiteSpace(hallTicketNumber))
            {
                ModelState.AddModelError("", "Please enter a valid Hall Ticket Number.");
                return View("Index");
            }

            hallTicketNumber = hallTicketNumber.Trim().ToUpper();
            
            // Check if hall ticket matches expected format
            if (hallTicketNumber.Length != 10)
            {
                ModelState.AddModelError("", "Invalid Hall Ticket Number. Must be exactly 10 characters.");
                return View("Index");
            }

            // Construct student details from Wizard selection
            var student = new Student
            {
                HallTicket = hallTicketNumber,
                Course = course,
                Branch = $"{branch} ({regulation})"
            };

            // Save basic student structure in DB if not already existing
            var existingStudent = _dbHelper.GetStudent(hallTicketNumber);
            if (existingStudent == null)
            {
                _dbHelper.SaveStudent(student);
            }
            else
            {
                // Update branch and course if they changed
                existingStudent.Course = course;
                existingStudent.Branch = $"{branch} ({regulation})";
                _dbHelper.SaveStudent(existingStudent);
            }

            // Redirect to Student Profile page carrying search preferences
            return RedirectToAction("StudentProfile", new { 
                hallTicket = hallTicketNumber, 
                targetYear = targetYear, 
                examType = examType,
                joinYear = joinYear,
                completionYear = completionYear,
                showStatus = showStatus
            });
        }

        /// <summary>
        /// Maps user-facing course names to the search strings used by JNTUA's API.
        /// JNTUA titles use abbreviated forms like "B.Pharm" and "M.Pharm".
        /// </summary>
        private string GetApiSearchString(string course)
        {
            return course switch
            {
                "B.Pharmacy" => "B.Pharm",
                "M.Pharmacy" => "M.Pharm",
                _ => course  // B.Tech, M.Tech, MCA, MBA already match
            };
        }

        // GET: /Results/StudentProfile
        public async Task<IActionResult> StudentProfile(string hallTicket, string targetYear = "All", string examType = "Both", int joinYear = 2020, int completionYear = 2024, string showStatus = "Both")
        {
            if (string.IsNullOrWhiteSpace(hallTicket))
            {
                return RedirectToAction("Index");
            }

            hallTicket = hallTicket.Trim().ToUpper();
            var student = _dbHelper.GetStudent(hallTicket);

            if (student == null)
            {
                return RedirectToAction("Index");
            }

            // Get consolidated passed results from local DB
            var dbResults = _dbHelper.GetConsolidatedResults(hallTicket);
            var allResults = _dbHelper.GetAllResults(hallTicket);

            // Fetch available result sets from JNTUA results API matching the student's Course & Regulation
            var matchingExamSheets = new List<ResultSet>();
            bool apiConnectionFailed = false;
            
            // Use the correct API search string for the student's course
            var apiSearchTerm = GetApiSearchString(student.Course);

            // Fetch multiple pages to ensure all past, current, and newly uploaded exams are retrieved
            for (int page = 1; page <= 8; page++)
            {
                var apiResponse = await _apiService.GetResultSetsAsync(apiSearchTerm, page, 250);
                if (apiResponse == null)
                {
                    // API connection failed (timeout, network error, etc.)
                    apiConnectionFailed = true;
                    break;
                }
                if (apiResponse.Data?.Responses == null || apiResponse.Data.Responses.Count == 0)
                {
                    break;
                }

                // Extract student regulation (e.g., R20)
                var regulationMatch = Regex.Match(student.Branch, @"\((R\d+)\)");
                var studentReg = regulationMatch.Success ? regulationMatch.Groups[1].Value : "R20";

                foreach (var dto in apiResponse.Data.Responses)
                {
                    var parsedExam = ParseExamTitle(dto.Title);
                    if (parsedExam != null)
                    {
                        parsedExam.Id = !string.IsNullOrEmpty(dto.ResultTitleId) ? dto.ResultTitleId : dto.Id;
                        if (DateTime.TryParse(dto.PublishDate, out var pubDate))
                        {
                            parsedExam.PublishDate = pubDate;
                        }

                        // Apply Wizard Filters:
                        // 1. Course match (e.g. B.Tech)
                        // 2. Regulation match (e.g. R20)
                        // 3. Target Year match (1st, 2nd, 3rd, 4th, or All)
                        // 4. Exam Type match (Regular, Supplementary, or Both)
                        
                        bool courseMatch = parsedExam.Course.Equals(student.Course, StringComparison.OrdinalIgnoreCase);
                        bool regMatch = parsedExam.Regulation.Equals(studentReg, StringComparison.OrdinalIgnoreCase);

                        bool targetYearMatch = true;
                        if (targetYear != "All")
                        {
                            int targetYearInt = targetYear switch
                            {
                                "1" => 1,
                                "2" => 2,
                                "3" => 3,
                                "4" => 4,
                                _ => 0
                            };
                            targetYearMatch = parsedExam.Year == targetYearInt;
                        }

                        bool examTypeMatch = true;
                        if (examType == "Regular")
                        {
                            examTypeMatch = parsedExam.Title.Contains("Regular", StringComparison.OrdinalIgnoreCase);
                        }
                        else if (examType == "Supplementary")
                        {
                            examTypeMatch = parsedExam.Title.Contains("Supplementary", StringComparison.OrdinalIgnoreCase) || 
                                            parsedExam.Title.Contains("Suppl", StringComparison.OrdinalIgnoreCase);
                        }

                        if (courseMatch && regMatch && targetYearMatch && examTypeMatch)
                        {
                            matchingExamSheets.Add(parsedExam);
                        }
                    }
                }
            }

            // Identify cached exams and unfetched exams
            var fetchedIds = _dbHelper.GetFetchedResultSetIds(hallTicket);
            var unfetchedExams = matchingExamSheets.Where(e => !fetchedIds.Contains(e.Id)).OrderBy(e => e.Year).ThenBy(e => e.Semester).ToList();
            var fetchedExams = matchingExamSheets.Where(e => fetchedIds.Contains(e.Id)).OrderBy(e => e.Year).ThenBy(e => e.Semester).ToList();

            // Populate view properties
            ViewBag.Student = student;
            ViewBag.ConsolidatedResults = dbResults;
            ViewBag.AllResults = allResults;
            ViewBag.UnfetchedExams = unfetchedExams;
            ViewBag.FetchedExams = fetchedExams;
            ViewBag.TargetYearFilter = targetYear;
            ViewBag.ExamTypeFilter = examType;
            ViewBag.JoinYearFilter = joinYear;
            ViewBag.CompletionYearFilter = completionYear;
            ViewBag.ShowStatus = showStatus;
            ViewBag.ApiConnectionFailed = apiConnectionFailed;
            ViewBag.TotalMatchingSheets = matchingExamSheets.Count;

            // Compute GPA statistics
            ViewBag.CGPA = CalculateCGPA(dbResults);
            ViewBag.TotalCredits = dbResults.Where(r => r.ResultStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase)).Sum(r => r.Credits ?? 0);
            ViewBag.BacklogsCount = dbResults.Count(r => !r.ResultStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase));

            return View();
        }

        // POST: /Results/FetchResult
        [HttpPost]
        public async Task<JsonResult> FetchResult(string resultTitleId, string hallTicketNumber, string verificationToken, string showStatus = "Both")
        {
            if (string.IsNullOrWhiteSpace(resultTitleId) || string.IsNullOrWhiteSpace(hallTicketNumber))
            {
                return Json(new { success = false, message = "Missing result ID or Hall Ticket." });
            }

            hallTicketNumber = hallTicketNumber.Trim().ToUpper();

            // Fetch result metadata first to get exam details
            var metaResponse = await _apiService.GetResultSetDetailAsync(resultTitleId);
            if (metaResponse?.Data == null)
            {
                return Json(new { success = false, message = "Could not fetch exam metadata from JNTUA." });
            }

            var meta = metaResponse.Data;
            // Use the student's actual course as fallback instead of hardcoded B.Tech
            var studentForFallback = _dbHelper.GetStudent(hallTicketNumber);
            var fallbackCourse = studentForFallback?.Course ?? "B.Tech";
            var fallbackReg = "R20";
            if (studentForFallback != null)
            {
                var regFallback = Regex.Match(studentForFallback.Branch, @"\((R\d+)\)");
                if (regFallback.Success) fallbackReg = regFallback.Groups[1].Value;
            }
            var parsedExam = ParseExamTitle(meta.Title) ?? new ResultSet
            {
                Id = resultTitleId,
                Title = meta.Title,
                Course = fallbackCourse,
                Regulation = fallbackReg,
                Year = 1,
                Semester = 1,
                ExamType = "Regular"
            };
            parsedExam.Id = resultTitleId;
            if (DateTime.TryParse(meta.PublishDate, out var pubDate))
            {
                parsedExam.PublishDate = pubDate;
            }

            // Fetch student marks from JNTUA API
            var result = await _apiService.SearchStudentResultAsync(resultTitleId, hallTicketNumber, verificationToken);
            if (!result.Success || result.Data == null || result.Data.Count == 0)
            {
                return Json(new { success = false, message = result.ErrorMessage });
            }

            // Save Student info name if we fetch it for the first time
            var studentName = result.Data.First().StudentName;
            var student = _dbHelper.GetStudent(hallTicketNumber);
            if (student != null && (string.IsNullOrEmpty(student.StudentName) || student.StudentName == "Unfetched Student Profile"))
            {
                student.StudentName = studentName;
                _dbHelper.SaveStudent(student);
            }

            // Parse subject results
            var studentResults = new List<StudentResult>();
            foreach (var item in result.Data)
            {
                var internalMarks = ParseInt(GetValueCaseInsensitive(item.MarksObject, "internal marks"));
                var externalMarks = ParseInt(GetValueCaseInsensitive(item.MarksObject, "external marks"));
                var totalMarks = ParseInt(GetValueCaseInsensitive(item.MarksObject, "total marks"));
                var grade = GetValueCaseInsensitive(item.MarksObject, "grade");
                var credits = ParseDecimal(GetValueCaseInsensitive(item.MarksObject, "credits"));
                var status = GetValueCaseInsensitive(item.MarksObject, "resultstatus");
                
                if (string.IsNullOrEmpty(status))
                {
                    status = (grade.ToUpper() != "F" && grade.ToUpper() != "AB" && grade.ToUpper() != "F(AB)" && grade.ToUpper() != "ABSENT") ? "Pass" : "Fail";
                }

                studentResults.Add(new StudentResult
                {
                    HallTicket = hallTicketNumber,
                    ResultSetId = resultTitleId,
                    SubjectCode = item.SubjectCode,
                    SubjectName = item.SubjectName,
                    InternalMarks = internalMarks,
                    ExternalMarks = externalMarks,
                    TotalMarks = totalMarks,
                    Grade = grade,
                    Credits = credits,
                    ResultStatus = status,
                    RawMarksJson = JsonSerializer.Serialize(item.MarksObject)
                });
            }

            // ─── USER SPECIFICATION FILTER ───
            // "download results that student has passed in even in 1 subject. if student failed all then not download it."
            bool hasAnyPass = studentResults.Any(r => r.ResultStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase));
            bool hasAnyFail = studentResults.Any(r => !r.ResultStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase));
            
            bool shouldSave = false;
            string skipReason = "";

            if (showStatus == "Passed")
            {
                shouldSave = hasAnyPass;
                skipReason = "Failed all subjects or Absent (Passed Only filter)";
            }
            else if (showStatus == "Failed")
            {
                shouldSave = hasAnyFail;
                skipReason = "Passed all subjects (Failed Only filter)";
            }
            else // Both
            {
                shouldSave = true; // Always save for "Both Passed & Failed"
            }
            
            if (shouldSave)
            {
                // Save ResultSet info
                _dbHelper.SaveResultSet(parsedExam);
                // Save Results
                _dbHelper.SaveStudentResults(studentResults);
                
                return Json(new { 
                    success = true, 
                    downloaded = true, 
                    studentName = studentName, 
                    examTitle = parsedExam.Title,
                    passedCount = studentResults.Count(r => r.ResultStatus.Equals("Pass", StringComparison.OrdinalIgnoreCase)),
                    totalCount = studentResults.Count
                });
            }
            else
            {
                return Json(new { 
                    success = true, 
                    downloaded = false, 
                    studentName = studentName, 
                    examTitle = parsedExam.Title,
                    message = skipReason
                });
            }
        }

        // GET: /Results/OriginalMemo
        public IActionResult OriginalMemo(string hallTicket, string resultSetId)
        {
            if (string.IsNullOrWhiteSpace(hallTicket) || string.IsNullOrWhiteSpace(resultSetId))
            {
                return RedirectToAction("Index");
            }
            
            var student = _dbHelper.GetStudent(hallTicket);
            var resultSet = _dbHelper.GetResultSet(resultSetId);
            var results = _dbHelper.GetStudentResultsForResultSet(hallTicket, resultSetId);
            
            if (student == null || resultSet == null || results.Count == 0)
            {
                return Content("No memo records found in database.");
            }
            
            ViewBag.Student = student;
            ViewBag.ResultSet = resultSet;
            
            return View(results);
        }

        [HttpGet]
        public async Task<IActionResult> DebugFilters(string course = "B.Tech", string examType = "Regular")
        {
            var apiResponse = await _apiService.GetResultSetsAsync(course, 1, 200);
            var results = new List<object>();
            if (apiResponse?.Data?.Responses != null)
            {
                foreach (var dto in apiResponse.Data.Responses)
                {
                    var parsedExam = ParseExamTitle(dto.Title);
                    if (parsedExam != null)
                    {
                        bool examTypeMatch = true;
                        if (examType == "Regular")
                        {
                            examTypeMatch = parsedExam.Title.Contains("Regular", StringComparison.OrdinalIgnoreCase);
                        }
                        else if (examType == "Supplementary")
                        {
                            examTypeMatch = parsedExam.Title.Contains("Supplementary", StringComparison.OrdinalIgnoreCase) || 
                                            parsedExam.Title.Contains("Suppl", StringComparison.OrdinalIgnoreCase);
                        }
                        results.Add(new {
                            title = dto.Title,
                            pubDate = dto.PublishDate,
                            parsedYear = parsedExam.Year,
                            parsedReg = parsedExam.Regulation,
                            examTypeMatch = examTypeMatch
                        });
                    }
                }
            }
            return Json(results);
        }

        #region Helpers

        private ResultSet? ParseExamTitle(string title)
        {
            var r = new ResultSet { Title = title };

            // Course detection
            if (title.Contains("B.Tech", StringComparison.OrdinalIgnoreCase)) r.Course = "B.Tech";
            else if (title.Contains("B.Pharm", StringComparison.OrdinalIgnoreCase)) r.Course = "B.Pharmacy";
            else if (title.Contains("M.Tech", StringComparison.OrdinalIgnoreCase)) r.Course = "M.Tech";
            else if (title.Contains("M.Pharm", StringComparison.OrdinalIgnoreCase)) r.Course = "M.Pharmacy";
            else if (title.Contains("MCA", StringComparison.OrdinalIgnoreCase)) r.Course = "MCA";
            else if (title.Contains("MBA", StringComparison.OrdinalIgnoreCase)) r.Course = "MBA";
            else return null;

            // Regulation detection (e.g. "(R20)")
            var regMatch = Regex.Match(title, @"\((R\d+)\)");
            r.Regulation = regMatch.Success ? regMatch.Groups[1].Value : "R20";

            // Year and Semester detection
            if (title.Contains("Year", StringComparison.OrdinalIgnoreCase))
            {
                if (title.Contains("IV Year", StringComparison.OrdinalIgnoreCase) || title.Contains("4 Year", StringComparison.OrdinalIgnoreCase)) r.Year = 4;
                else if (title.Contains("III Year", StringComparison.OrdinalIgnoreCase) || title.Contains("3 Year", StringComparison.OrdinalIgnoreCase)) r.Year = 3;
                else if (title.Contains("II Year", StringComparison.OrdinalIgnoreCase) || title.Contains("2 Year", StringComparison.OrdinalIgnoreCase)) r.Year = 2;
                else r.Year = 1;

                if (title.Contains("II Semester", StringComparison.OrdinalIgnoreCase) || title.Contains("2 Semester", StringComparison.OrdinalIgnoreCase)) r.Semester = 2;
                else r.Semester = 1;
            }
            else
            {
                // PG/Semesters courses without "Year" word in their titles
                int absoluteSemester = 1;
                if (title.Contains("IV Semester", StringComparison.OrdinalIgnoreCase) || title.Contains("4 Semester", StringComparison.OrdinalIgnoreCase)) absoluteSemester = 4;
                else if (title.Contains("III Semester", StringComparison.OrdinalIgnoreCase) || title.Contains("3 Semester", StringComparison.OrdinalIgnoreCase)) absoluteSemester = 3;
                else if (title.Contains("II Semester", StringComparison.OrdinalIgnoreCase) || title.Contains("2 Semester", StringComparison.OrdinalIgnoreCase)) absoluteSemester = 2;
                
                r.Year = (absoluteSemester - 1) / 2 + 1;
                r.Semester = (absoluteSemester - 1) % 2 + 1;
            }

            // Exam Type detection
            if (title.Contains("Regular", StringComparison.OrdinalIgnoreCase))
            {
                r.ExamType = "Regular";
            }
            else
            {
                r.ExamType = "Supplementary";
            }

            return r;
        }

        private double CalculateCGPA(List<ConsolidatedResult> results)
        {
            double totalPoints = 0;
            double totalCredits = 0;

            foreach (var item in results)
            {
                if (item.Credits.HasValue)
                {
                    // Audit / Non-credit courses are not included in SGPA/CGPA calculation (credits = 0)
                    if (item.Credits.Value == 0) continue;

                    var points = GradeToPoints(item.Grade);
                    var credits = (double)item.Credits.Value;
                    
                    totalPoints += (points * credits);
                    totalCredits += credits;
                }
            }

            return totalCredits > 0 ? Math.Round(totalPoints / totalCredits, 2) : 0;
        }

        private int GradeToPoints(string grade)
        {
            if (string.IsNullOrWhiteSpace(grade)) return 0;
            
            switch (grade.Trim().ToUpper())
            {
                case "S":
                case "O":
                case "A+": return 10;
                case "A": return 9;
                case "B": return 8;
                case "C": return 7;
                case "D": return 6;
                case "E": return 5;
                default: return 0; // Fail (F), Absent (Ab), etc. carry 0 points
            }
        }

        private string GetValueCaseInsensitive(Dictionary<string, string> dict, string key)
        {
            if (dict == null) return string.Empty;
            var matchKey = dict.Keys.FirstOrDefault(k => k.Equals(key, StringComparison.OrdinalIgnoreCase));
            return matchKey != null ? dict[matchKey] : string.Empty;
        }

        private int? ParseInt(string val)
        {
            if (int.TryParse(val, out var res)) return res;
            return null;
        }

        private decimal? ParseDecimal(string val)
        {
            if (decimal.TryParse(val, out var res)) return res;
            return null;
        }

        #endregion
    }
}
