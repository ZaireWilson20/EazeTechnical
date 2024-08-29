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
    private readonly JobRequestDto DefaultParams;

    public JobPostingController(IJobPostingScraper jobScraper, ILogger<JobPostingController> logger)
    {
        _jobScraper = jobScraper;
        _logger = logger;
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

            var response = new QueryResponse
            {
                Metadata = new Metadata
                {
                    QueryId = "1"  // TODO: Make sure this unique for each new query made
                },
                Results = jobPostingDtos,
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
}
