using System.Text.Json;
using Microsoft.Extensions.Logging;
using EazeTechnical.Controllers;
using EazeTechnical.Data;
using EazeTechnical.Services;
using EazeTechnical.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace JobScraperApi.Tests;

public class JobsPostingControllerTests
{
private readonly Mock<IJobPostingScraper> _mockJobScraper;
    private readonly Mock<ILogger<JobPostingController>> _mockLogger;
    private readonly Mock<ILogger<JobsDbContext>> _mockDbContextLogger;  // Mock logger for JobsDbContext
    private readonly JobPostingController _controller;
    private readonly JobsDbContext _dbContext;
    private readonly Mock<JobsDbContext> _mockDbContext;
    


    public JobsPostingControllerTests()
    {
        _mockJobScraper = new Mock<IJobPostingScraper>();
        _mockLogger = new Mock<ILogger<JobPostingController>>();
        _mockDbContextLogger = new Mock<ILogger<JobsDbContext>>();
        _mockDbContext = new Mock<JobsDbContext>();
        // Setup in-memory database context
        var options = new DbContextOptionsBuilder<JobsDbContext>()
            .UseInMemoryDatabase(databaseName: "TestDatabase")
            .Options;
        _dbContext = new JobsDbContext(options, _mockDbContextLogger.Object);
        _controller = new JobPostingController(_mockJobScraper.Object, _mockLogger.Object, _dbContext);
        
        SeedDatabase();
    }
    
    
    private void SeedDatabase()
    {
        // Reset
        _dbContext.JobPostQueries.RemoveRange(_dbContext.JobPostQueries);
        _dbContext.SaveChanges();
        
        var initialData = new List<QueryResponse>
        {
            new() { Id = 1, Results = JsonSerializer.Serialize(new List<JobPostingDto>()) },
            new() { Id = 2, Results = JsonSerializer.Serialize(new List<JobPostingDto>()) },
            new() { Id = 3, Results = JsonSerializer.Serialize(new List<JobPostingDto>()) },
            new() { Id = 4, Results = JsonSerializer.Serialize(new List<JobPostingDto>()) },
            new() { Id = 5, Results = JsonSerializer.Serialize(new List<JobPostingDto>()) },
            new() { Id = 6, Results = JsonSerializer.Serialize(new List<JobPostingDto>()) },
        };

        _dbContext.JobPostQueries.AddRange(initialData);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task FindJobsWithParams_ValidInput_ReturnsOkResult()
    {
        // Arrange
        var jobRequest = new JobRequestDto { Query = "farm", Location = "Idaho", LastNdays = 7 };
        var website = "indeed.com";
        var jobPostings = new List<JobPostingDto> { new() }; 

        _mockJobScraper
            .Setup(s => s.ScrapeJobsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tuple<IEnumerable<JobPostingDto>, bool>(jobPostings, true));

        _controller.ModelState.Clear(); // Ensure ModelState is valid

        // Act
        var result = await _controller.FindJobsWithParams(jobRequest, website);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseValue = Assert.IsType<QueryResponseDto>(okResult.Value);
        Assert.Equal(jobPostings.Count, responseValue.Results.Count);
    }

    [Fact]
    public async Task FindJobsWithParams_InvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var jobRequest = new JobRequestDto();
        var website = "indeed";

        _controller.ModelState.AddModelError("Query", "Required");

        // Act
        var result = await _controller.FindJobsWithParams(jobRequest, website);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task FindJobsWithParams_Timeout_ReturnsRequestTimeout()
    {
        // Arrange
        var jobRequest = new JobRequestDto { Query = "developer", Location = "NY", LastNdays = 7 };
        var website = "indeed";

        _mockJobScraper
            .Setup(s => s.ScrapeJobsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await _controller.FindJobsWithParams(jobRequest, website);

        // Assert
        var timeoutResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status408RequestTimeout, timeoutResult.StatusCode);
    }

    [Fact]
    public async Task FindJobsWithParams_ScraperException_ReturnsInternalServerError()
    {
        // Arrange
        var jobRequest = new JobRequestDto { Query = "developer", Location = "NY", LastNdays = 7 };
        var website = "indeed";

        _mockJobScraper
            .Setup(s => s.ScrapeJobsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Scraper error"));

        // Act
        var result = await _controller.FindJobsWithParams(jobRequest, website);

        // Assert
        var errorResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status500InternalServerError, errorResult.StatusCode);
    }
    
    [Fact]
    public async Task FindJobsWithParams_DbSaveFails_SetsQueryIdToMinusOne()
    {
        // Arrange
        var jobRequest = new JobRequestDto { Query = "developer", Location = "NY", LastNdays = 7 };
        var website = "indeed";
        var jobPostings = new List<JobPostingDto> { new JobPostingDto() };

        _mockJobScraper
            .Setup(s => s.ScrapeJobsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Tuple<IEnumerable<JobPostingDto>, bool>(jobPostings, true));

        // Simulate a database save failure by throwing an exception
        _controller.BadDbTestFailFlag = true;
        
        // Act
        var result = await _controller.FindJobsWithParams(jobRequest, website);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseValue = Assert.IsType<QueryResponseDto>(okResult.Value);
        Assert.Equal(-1, responseValue.Metadata.QueryId);
    }
}