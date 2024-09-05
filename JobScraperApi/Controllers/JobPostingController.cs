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
    private JobsDbContext _jobsDbContext;
    private readonly IJobPostingScraper _jobScraper;
    private readonly ILogger<JobPostingController> _logger;

    public bool BadDbTestFailFlag = false;

    public JobPostingController(IJobPostingScraper jobScraper, ILogger<JobPostingController> logger, JobsDbContext dataContext)
    {
        _jobScraper = jobScraper;
        _logger = logger;
        _jobsDbContext = dataContext;
    }
    
    [HttpPost]
    public async Task<ObjectResult> FindJobsWithParams([FromBody]JobRequestDto jobPostParams, string website)
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

            var responseData = new QueryResponse
            {
                Results = JsonSerializer.Serialize(jobPostingDtos),
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
                dbJobPostings = JsonSerializer.Deserialize<List<JobPostingDto>>(dbQueryRow.Results);
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
            
            List<JobPostingDto>? dbJobPostings = null;
            if (jobListing != null)
            {
                dbJobPostings = JsonSerializer.Deserialize<List<JobPostingDto>>(jobListing.Results);
            }
            var response = new QueryResponseDto
            {
                Metadata = new Metadata
                {
                    QueryId = query_id
                },
                Results = dbJobPostings,
                Message = dbJobPostings != null ? ProjectConstants.ResponseSuccessMessages.FindSuccessResponse200 : ProjectConstants.ResponseWarningMessages.FindByIdWarningResponse200
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
}
