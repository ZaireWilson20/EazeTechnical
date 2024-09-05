using System.Text.Json;
using EazeTechnical.Data;
using EazeTechnical.Models;
using EazeTechnical.Services;
using EazeTechnical.Utilities;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace EazeTechnical.Controllers;


[Route("/scrape/{website}")]
[ApiController]
public class JobPostingController : Controller
{
    private readonly JobsDbContext _jobsDbContext;
    private readonly IJobPostingScraper _jobScraper;
    private readonly ILogger<JobPostingController> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public bool BadDbTestFailFlag = false;

    public JobPostingController(IJobPostingScraper jobScraper, ILogger<JobPostingController> logger, JobsDbContext dataContext, JsonSerializerOptions jsonSerializerOptions)
    {
        _jobScraper = jobScraper;
        _logger = logger;
        _jobsDbContext = dataContext;
        _jsonSerializerOptions = jsonSerializerOptions;
    }

    [HttpPost]
    public async Task<ObjectResult> FindJobsWithParams([FromBody] JobRequestDto jobPostParams, string website)
    {
        if (_jobsDbContext == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ProjectConstants.ResponseErrorMessages.DatabaseNoContext500);
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Set defaults for missing params
        var queryStr = jobPostParams.Query ?? ProjectConstants.JobRequestDefaultFields.Query;
        var locationStr = jobPostParams.Location ?? ProjectConstants.JobRequestDefaultFields.Location;
        var lastNdaysVal = jobPostParams.LastNdays ?? ProjectConstants.JobRequestDefaultFields.LastNdays;
        using var cancTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        try
        {
            var scrapedJobs = await _jobScraper.ScrapeJobsAsync(queryStr, locationStr, lastNdaysVal, cancTokenSource.Token);
            var jobPostingDtos = scrapedJobs.Item1.ToList();

            if (!TrySerializeJobPostings(jobPostingDtos, out var serializedJobPostings))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ProjectConstants.ResponseErrorMessages.GenericInternalError500);
            }

            var responseData = new QueryResponse
            {
                Results = serializedJobPostings ?? ProjectConstants.GeneralUseString.EmptyJsonObj,
            };

            QueryResponse? dbQueryRow = null;
            try
            {
                dbQueryRow = await SaveDataAsync(responseData);
            }
            catch (Exception ex)
            {
                _logger.LogError("Saving to DB Exception: {} - Stack Trace: {}", ex.Message, ex.StackTrace);
            }

            List<JobPostingDto>? dbJobPostings = null;
            if (dbQueryRow != null)
            {
                if (!TryDeserializeJobPostings(dbQueryRow.Results, out dbJobPostings))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, ProjectConstants.ResponseErrorMessages.GenericInternalError500);
                }            
            }

            var response = new QueryResponseDto
            {
                Metadata = new Metadata
                {
                    QueryId = dbQueryRow?.Id ?? -1
                },
                Results = dbJobPostings ?? jobPostingDtos,
                Message = scrapedJobs.Item2 ? ProjectConstants.ResponseSuccessMessages.FindSuccessResponse200 : ProjectConstants.ResponseWarningMessages.MissingJobsFindWarningResponse200
            };

            return Ok(response);

        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, ProjectConstants.ResponseErrorMessages.ScrapeRequestTimeout408);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, $"{ProjectConstants.ResponseErrorMessages.GenericInternalError500}: {ex.Message}");
        }
    }

    [HttpGet("{query_id:int}")]
    public async Task<ObjectResult> FindSrapeResultsFromQueryId(int query_id)
    {
        try
        {
            var jobListing = await _jobsDbContext.JobPostQueries.FindAsync(query_id);
            
            // Query not found in database
            if (jobListing == null)
            {
                return StatusCode(StatusCodes.Status404NotFound,
                    ProjectConstants.ResponseErrorMessages.QueryIdNotInDatabase404);
            }

            if (!TryDeserializeJobPostings(jobListing.Results, out var dbJobPostings) || dbJobPostings == null)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, ProjectConstants.ResponseErrorMessages.GenericInternalError500);
            }

            var response = new QueryResponseDto
            {
                Metadata = new Metadata { QueryId = query_id },
                Results = dbJobPostings,
                Message = ProjectConstants.ResponseSuccessMessages.FindSuccessResponse200
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, $"{ProjectConstants.ResponseErrorMessages.GenericInternalError500}: {ex.Message}");
        }
    }
    public async Task<QueryResponse> SaveDataAsync(QueryResponse model)
    {
        try
        {
            if (BadDbTestFailFlag)
            {
                throw new Exception();
            }
            _jobsDbContext.JobPostQueries.Add(model);

            await _jobsDbContext.SaveChangesAsync();
        }
        catch
        {
            throw new NpgsqlException();
        }

        return model;
    }
    
    private bool TryDeserializeJobPostings(string jsonString, out List<JobPostingDto>? jobPostings)
    {
        jobPostings = null;
        try
        {
            jobPostings = JsonSerializer.Deserialize<List<JobPostingDto>>(jsonString, _jsonSerializerOptions);
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogError("JSON parsing error: {Message}", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError("Invalid operation during deserialization: {Message}", ex.Message);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError("Null argument provided: {Message}", ex.Message);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError("Type not supported for deserialization: {Message}", ex.Message);
        }
        catch (FormatException ex)
        {
            _logger.LogError("Invalid JSON format: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error during deserialization: {Message}", ex.Message);
        }
        return false;
    }
    
    private bool TrySerializeJobPostings(List<JobPostingDto> jobPostings, out string? jsonString)
    {
        jsonString = null;
        try
        {
            jsonString = JsonSerializer.Serialize(jobPostings, _jsonSerializerOptions);
            return true;
        }
        catch (JsonException ex)
        {
            _logger.LogError("JSON serialization error: {Message}", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError("Invalid operation during serialization: {Message}", ex.Message);
        }
        catch (ArgumentNullException ex)
        {
            _logger.LogError("Null argument provided: {Message}", ex.Message);
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError("Type not supported for serialization: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected error during serialization: {Message}", ex.Message);
        }
        return false;
    }
}