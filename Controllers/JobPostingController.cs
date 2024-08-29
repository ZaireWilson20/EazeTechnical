using EazeTechnical.Data;
using EazeTechnical.Models;
using EazeTechnical.Services;
using Microsoft.AspNetCore.Mvc;

namespace EazeTechnical.Controllers;


[Route("/scrape/{website}")]
[ApiController]
public class JobPostingController : Controller
{
    private JobsDbContext? _jobsDbContext =>
        (JobsDbContext?)HttpContext.RequestServices.GetService(typeof(JobsDbContext));
    private readonly IJobPostingScraper _jobScraper;
    private readonly ILogger<JobPostingController> _logger;

    public JobPostingController(IJobPostingScraper jobScraper, ILogger<JobPostingController> logger)
    {
        _jobScraper = jobScraper;
        _logger = logger;
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
        var queryStr = jobPostParams.Query ?? "Cannabis";
        var locationStr = jobPostParams.Location ?? "California";
        var lastNdaysVal = jobPostParams.LastNdays ?? -1;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        
        try
        {
            var scrapedJobs = await _jobScraper.ScrapeJobsAsync(queryStr, locationStr, lastNdaysVal, cts.Token);
            var jobPostingDtos = scrapedJobs.ToList();
            foreach (var job in jobPostingDtos) //TODO: Remove
            {
                _logger.LogInformation("Job: Title {}", job.Title);
            }
            // Create the response
            var response = new QueryResponse
            {
                Metadata = new Metadata
                {
                    QueryId = "1"  // TODO: Make sure this unique for each new query made
                },
                Results = jobPostingDtos
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
}
