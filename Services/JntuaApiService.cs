using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace JntuaResultsFilter.Services
{
    public class JntuaApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<JntuaApiService> _logger;
        private const string BaseUrl = "https://jntuaresults.ac.in/app/api/v1";

        public JntuaApiService(HttpClient httpClient, ILogger<JntuaApiService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Set headers to mimic a normal browser request
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/plain, */*");
            _httpClient.DefaultRequestHeaders.Referrer = new Uri("https://jntuaresults.ac.in/");
        }

        // 1. Fetch result sets matching a search query
        public async Task<JntuaResultSetsResponse?> GetResultSetsAsync(string searchString, int pageNumber = 1, int pageSize = 100)
        {
            try
            {
                var url = $"{BaseUrl}/student/resultSets?searchString={Uri.EscapeDataString(searchString)}&pageNumber={pageNumber}&pageSize={pageSize}";
                _logger.LogInformation("Fetching JNTUA ResultSets from: {Url}", url);
                
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch ResultSets. JNTUA returned: {Code} {Reason}", response.StatusCode, response.ReasonPhrase);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                return JsonSerializer.Deserialize<JntuaResultSetsResponse>(content, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while fetching JNTUA ResultSets");
                return null;
            }
        }

        // 2. Fetch result set metadata (to get column definitions and title details)
        public async Task<JntuaResultSetDetailResponse?> GetResultSetDetailAsync(string resultTitleId)
        {
            try
            {
                var url = $"{BaseUrl}/student/resultSet?resultTitleId={Uri.EscapeDataString(resultTitleId)}";
                _logger.LogInformation("Fetching JNTUA ResultSet detail from: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to fetch ResultSet detail. Status: {Code}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                return JsonSerializer.Deserialize<JntuaResultSetDetailResponse>(content, options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while fetching ResultSet detail");
                return null;
            }
        }

        // 3. Search and fetch student marks for a given result set
        public async Task<JntuaSearchResultResponse> SearchStudentResultAsync(string resultTitleId, string hallTicketNumber, string verificationToken)
        {
            try
            {
                var url = $"{BaseUrl}/student/searchResult";
                _logger.LogInformation("Posting JNTUA SearchResult for HT: {HT}, ResultSet: {Id}", hallTicketNumber, resultTitleId);

                var payload = new
                {
                    resultTitleId = resultTitleId,
                    hallTicketNumber = hallTicketNumber,
                    verificationToken = verificationToken
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("JNTUA searchResult returned error status: {Code}, Body: {Body}", response.StatusCode, responseContent);
                    try
                    {
                        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                        var errResp = JsonSerializer.Deserialize<JntuaErrorResponse>(responseContent, options);
                        return new JntuaSearchResultResponse
                        {
                            Success = false,
                            ErrorMessage = errResp?.StatusMessage ?? "Bad Request from JNTUA server"
                        };
                    }
                    catch
                    {
                        return new JntuaSearchResultResponse
                        {
                            Success = false,
                            ErrorMessage = $"HTTP {response.StatusCode}: {response.ReasonPhrase}"
                        };
                    }
                }

                var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var apiResponse = JsonSerializer.Deserialize<JntuaSearchApiResponse>(responseContent, jsonOptions);

                if (apiResponse != null && apiResponse.StatusCode == 200 && apiResponse.Data != null)
                {
                    return new JntuaSearchResultResponse
                    {
                        Success = true,
                        Data = apiResponse.Data
                    };
                }
                else
                {
                    return new JntuaSearchResultResponse
                    {
                        Success = false,
                        ErrorMessage = apiResponse?.StatusMessage ?? "No results found or invalid response structure."
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while posting JNTUA SearchResult");
                return new JntuaSearchResultResponse
                {
                    Success = false,
                    ErrorMessage = $"Internal Error: {ex.Message}"
                };
            }
        }

        public async Task<byte[]?> DownloadLogoAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // Wikipedia requires a specific User-Agent with contact details to avoid HTTP 403/429
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("JntuaResultsAggregator/1.0 (contact: admin@jntuaresults.ac.in)");
                    
                    var url = "https://upload.wikimedia.org/wikipedia/en/thumb/e/e3/Jawaharlal_Nehru_Technological_University,_Anantapur_logo.png/120px-Jawaharlal_Nehru_Technological_University,_Anantapur_logo.png";
                    _logger.LogInformation("Attempting to download JNTUA logo from Wikipedia: {Url}", url);
                    
                    var response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        var bytes = await response.Content.ReadAsByteArrayAsync();
                        _logger.LogInformation("Successfully downloaded logo from Wikipedia, size: {Size} bytes", bytes.Length);
                        return bytes;
                    }
                    else
                    {
                        _logger.LogError("Failed to download logo from Wikipedia. Status: {Code}", response.StatusCode);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download logo from Wikipedia");
            }
            return null;
        }
    }

    #region API Response Contracts

    public class JntuaResultSetsResponse
    {
        [JsonPropertyName("status_code")]
        public int StatusCode { get; set; }
        [JsonPropertyName("status_message")]
        public string StatusMessage { get; set; } = string.Empty;
        public JntuaResultSetsData? Data { get; set; }
    }

    public class JntuaResultSetsData
    {
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public List<JntuaResultSetDto> Responses { get; set; } = new List<JntuaResultSetDto>();
    }

    public class JntuaResultSetDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;
    }

    public class JntuaResultSetDetailResponse
    {
        [JsonPropertyName("status_code")]
        public int StatusCode { get; set; }
        [JsonPropertyName("status_message")]
        public string StatusMessage { get; set; } = string.Empty;
        public JntuaResultSetDetailData? Data { get; set; }
    }

    public class JntuaResultSetDetailData
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string PublishDate { get; set; } = string.Empty;
        public string RevaluationInfo { get; set; } = string.Empty;
        public List<JntuaColumnDto> ResultTypeColumns { get; set; } = new List<JntuaColumnDto>();
    }

    public class JntuaColumnDto
    {
        public string ColumnName { get; set; } = string.Empty;
    }

    public class JntuaSearchApiResponse
    {
        [JsonPropertyName("status_code")]
        public int StatusCode { get; set; }
        [JsonPropertyName("status_message")]
        public string StatusMessage { get; set; } = string.Empty;
        public List<JntuaSubjectResultDto>? Data { get; set; }
    }

    public class JntuaSubjectResultDto
    {
        public string HallTicketNumber { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public string SubjectCode { get; set; } = string.Empty;
        public string SubjectName { get; set; } = string.Empty;
        public Dictionary<string, string> MarksObject { get; set; } = new Dictionary<string, string>();
    }

    public class JntuaErrorResponse
    {
        [JsonPropertyName("status_code")]
        public int StatusCode { get; set; }
        [JsonPropertyName("status_message")]
        public string StatusMessage { get; set; } = string.Empty;
    }

    public class JntuaSearchResultResponse
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<JntuaSubjectResultDto>? Data { get; set; }
    }

    #endregion
}
