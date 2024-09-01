using System.Text.Json;
using EazeTechnical.Data;
using EazeTechnical.Models;
using EazeTechnical.Services;
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
    private readonly JobRequestDto DefaultParams;

    public bool BadDbTestFailFlag = false;

    public JobPostingController(IJobPostingScraper jobScraper, ILogger<JobPostingController> logger, JobsDbContext dataContext)
    {
        _jobScraper = jobScraper;
        _logger = logger;
        _jobsDbContext = dataContext;
        DefaultParams = new JobRequestDto
        {
            Location = "California",
            Query = "Cannabis",
            LastNdays = -1
        };
    }
    
    [HttpPost]
    public async Task<ObjectResult> FindJobsWithParams([FromBody]JobRequestDto jobPostParams, string website)
    {
        if (_jobsDbContext == null)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, "Database context not available.");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Set defaults for missing params
        var queryStr = jobPostParams.Query ?? DefaultParams.Query;
        var locationStr = jobPostParams.Location ?? DefaultParams.Location;
        var lastNdaysVal = jobPostParams.LastNdays ?? DefaultParams.LastNdays;
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
                Message = scrapedJobs.Item2 ? "Full page scraped." : "Some job posts may be missing due to an exception hit while scraping."
            };

            return Ok(response);
            
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status408RequestTimeout, "The scraping operation timed out.");
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
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
                Message = dbJobPostings != null ? "Query Retrieved" : "The id provided does not exist."
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, $"An error occurred: {ex.Message}");
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
