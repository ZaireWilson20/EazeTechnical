using EazeTechnical.Models;
using Moq;
using EazeTechnical.Services;
using EazeTechnical.Utilities;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit.Abstractions;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace JobScraperApi.Tests;

public class JobScraperTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Mock<IWebDriver> _mockDriver;
    private readonly Mock<IWebDriverWait> _mockWebDriverWait;
    private readonly Mock<IWebElement> _mockElement;
    private readonly Mock<IFactory<IWebDriver>> _mockWebDriverFactory;
    private readonly Mock<IFactory<IWebDriverWait>> _mockWebDriverWaitFactory;
    private ChromeOptions _chromeOptions;

    public JobScraperTests(ITestOutputHelper testOutputHelper)
    {
        _chromeOptions = new ChromeOptions();
        _chromeOptions.AddArgument("--headless");
        _chromeOptions.AddArgument(
            "user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        _chromeOptions.AddArgument("--log-level=1");
        _chromeOptions.AddArgument("--disable-gpu");
        _chromeOptions.AddArgument("--no-sandbox");
        
        _testOutputHelper = testOutputHelper;
        _mockDriver = new Mock<IWebDriver>();
        _mockWebDriverWait = new Mock<IWebDriverWait>();
        _mockElement = new Mock<IWebElement>();
        _mockWebDriverFactory = new Mock<IFactory<IWebDriver>>();
        _mockWebDriverFactory.Setup(f => f.Create(_chromeOptions)).Returns(_mockDriver.Object);
        _mockWebDriverWaitFactory = new Mock<IFactory<IWebDriverWait>>();
        _mockWebDriverWaitFactory.Setup(f => f.Create(It.IsAny<IWebDriver>(), It.IsAny<TimeSpan>()))
            .Returns(_mockWebDriverWait.Object);

    }

    [Fact]
    public async Task ScrapeJobsAsync_ReturnsJobPostings()
    {
        // Arrange
        var logMessages = new List<string>();
        var logger = LoggerSetup(logMessages);
        var jobScraper = new JobScraper(logger.Object, _mockWebDriverFactory.Object, _mockWebDriverWaitFactory.Object, _chromeOptions);

        var mockedElementCollection = new List<IWebElement> { _mockElement.Object }.AsReadOnly();

        _mockDriver.Reset();
        _mockDriver.Setup(driver => driver.Navigate().GoToUrl(It.IsAny<string>()));
        // Mock to return a list of web elements as job postings
        _mockDriver.Setup(d => d.FindElements(By.XPath(ProjectConstants.Xpaths.CardContainerXPath)))
            .Returns(mockedElementCollection);

        _mockWebDriverWait.Reset();
        _mockWebDriverWait.Setup(wait => wait.Until(It.IsAny<Func<IWebDriver, IReadOnlyCollection<IWebElement>>>()))
            .Returns(mockedElementCollection);

        _mockElement.Reset();

        // Job posted within the last day
        _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobTimePostedXPath)).Text)
            .Returns(() => "1");

        _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobLocationXPath)).Text)
            .Returns(() => MockJobData.JobLocations[0]);
        _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobTitleXPath)).Text)
            .Returns(() => MockJobData.JobTitles[0]);
        _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobCompanyXPath)).Text)
            .Returns(() => MockJobData.JobCompanies[0]);
        _mockDriver.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.PageJobDescriptionXPath)).Text)
            .Returns(() => MockJobData.JobDescriptions[0]);
        _mockDriver.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.PageSalaryContainerXPath)).FindElement(By.XPath(ProjectConstants.Xpaths.LocalSpanPath)).Text)
            .Returns(() => MockJobData.JobSalaries[0]);

        _mockElement.Setup(elem => elem.Click());
        _mockDriver.Setup(driver => driver.Quit());

        // Act
        var result = await jobScraper.ScrapeJobsAsync("Software Engineer", "New York", 7, CancellationToken.None);

        // Assert
        PrintLogs(logMessages);
        Assert.NotEmpty(result.Item1);

        JobPostingDto jobResult = result.Item1.ToList()[0];

        Assert.Equal(jobResult.Title, MockJobData.JobTitles[0]);
        Assert.Equal(jobResult.Description, MockJobData.JobDescriptions[0]);
        Assert.Equal(jobResult.Salary, MockJobData.JobSalaries[0]);
        Assert.Equal(jobResult.Company, MockJobData.JobCompanies[0]);
        Assert.Equal(jobResult.Location, MockJobData.JobLocations[0]);
        Assert.True(result.Item2);
    }

    [Fact]
    public async Task ScrapeJobsAsync_JobCardElementNotFound_ReturnsScraperError()
    {
        // Arrange
        var logMessages = new List<string>();
        var logger = LoggerSetup(logMessages);

        var jobScraper = new JobScraper(logger.Object, _mockWebDriverFactory.Object, _mockWebDriverWaitFactory.Object, _chromeOptions);

        _mockDriver.Setup(d => d.Navigate().GoToUrl(It.IsAny<string>()));
        _mockDriver.Setup(d => d.FindElements(By.XPath(ProjectConstants.Xpaths.CardContainerXPath)))
            .Throws<NoSuchElementException>();

        // Act
        var result = await jobScraper.ScrapeJobsAsync("Software Engineer", "New York", 7, CancellationToken.None);

        PrintLogs(logMessages);
        // Assert
        Assert.False(result.Item2);
    }

    [Theory]
    [InlineData("Title")]
    [InlineData("Location")]
    [InlineData("CompanyName")]
    [InlineData("Salary")]
    [InlineData("Description")]
    public async Task ScrapeJobsAsync_JobFieldElementNotFound_ReturnsWithNullField(string field)
    {
        // Arrange

        var logMessages = new List<string>();
        var logger = LoggerSetup(logMessages);

        _mockDriver.Reset();
        _mockDriver.Setup(d => d.Navigate().GoToUrl(It.IsAny<string>()));

        _mockElement.Reset();
        // Job posted within the last day
        _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobTimePostedXPath)).Text)
            .Returns(() => "1");

        var mockedElementCollection = new List<IWebElement> { _mockElement.Object }.AsReadOnly();
        // Mock to return a list of (1) web elements as job postings
        _mockDriver.Setup(d => d.FindElements(By.XPath(ProjectConstants.Xpaths.CardContainerXPath)))
            .Returns(mockedElementCollection);

        // Mock the FindElement method to throw an exception for the field provided
        switch (field)
        {
            case "Title":
                _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobTitleXPath))).Throws(new NoSuchElementException());
                break;
            case "Location":
                _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobLocationXPath))).Throws(new NoSuchElementException());
                break;
            case "CompanyName":
                _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobCompanyXPath))).Throws(new NoSuchElementException());
                break;
            case "Salary":
                _mockDriver.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.PageSalaryContainerXPath))).Throws(new NoSuchElementException());
                break;
            case "Description":
                _mockDriver.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.PageJobDescriptionXPath))).Throws(new NoSuchElementException());
                break;
        }

        // Mock the FindElement method to return data for fields that were not provided
        if (field != "Location")
        {
            _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobLocationXPath)).Text)
                .Returns(() => MockJobData.JobLocations[0]);
        }

        if (field != "Title")
        {
            _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobTitleXPath)).Text)
                .Returns(() => MockJobData.JobTitles[0]);
        }

        if (field != "CompanyName")
        {
            _mockElement.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobCompanyXPath)).Text)
                .Returns(() => MockJobData.JobCompanies[0]);
        }

        if (field != "Description")
        {
            _mockDriver.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.PageJobDescriptionXPath)).Text)
                .Returns(() => MockJobData.JobDescriptions[0]);
        }

        if (field != "Salary")
        {
            _mockDriver.Setup(e => e.FindElement(By.XPath(ProjectConstants.Xpaths.PageSalaryContainerXPath)).FindElement(By.XPath(ProjectConstants.Xpaths.LocalSpanPath)).Text)
                .Returns(() => MockJobData.JobSalaries[0]);
        }

        _mockElement.Setup(e => e.Click());
        _mockDriver.Setup(d => d.Quit());

        _mockWebDriverWait.Reset();
        _mockWebDriverWait.Setup(wait => wait.Until(It.IsAny<Func<IWebDriver, IReadOnlyCollection<IWebElement>>>()))
            .Returns(mockedElementCollection);

        var jobScraper = new JobScraper(logger.Object, _mockWebDriverFactory.Object, _mockWebDriverWaitFactory.Object, _chromeOptions);

        // Act
        var result = await jobScraper.ScrapeJobsAsync("Software Engineer", "New York", 7, CancellationToken.None);

        // Assert

        PrintLogs(logMessages);

        // Should have data
        var jobPostings = result.Item1.ToList();
        Assert.NotEmpty(jobPostings);
        var jobPosting = jobPostings.First();

        // Given field should be null
        switch (field)
        {
            case "Title":
                Assert.Null(jobPosting.Title);
                break;
            case "Location":
                Assert.Null(jobPosting.Location);
                break;
            case "CompanyName":
                Assert.Null(jobPosting.Company);
                break;
            case "Salary":
                Assert.Null(jobPosting.Salary);
                break;
            case "Description":
                Assert.Null(jobPosting.Description);
                break;
        }

        // All other fields should not be null
        if (field != "Title")
            Assert.NotNull(jobPosting.Title);
        if (field != "Location")
            Assert.NotNull(jobPosting.Location);
        if (field != "CompanyName")
            Assert.NotNull(jobPosting.Company);
        if (field != "Description")
            Assert.NotNull(jobPosting.Description);
        if (field != "Salary")
            Assert.NotNull(jobPosting.Salary);

        Assert.True(result.Item2);
    }

    private Mock<ILogger<IJobPostingScraper>> LoggerSetup(List<string> logMessages)
    {
        var logger = new Mock<ILogger<IJobPostingScraper>>();

        // Capture log messages
        logger.Setup(log => log.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception, string>>((v, t) => true)!))
            .Callback<LogLevel, EventId, object, Exception, Delegate>((logLevel, eventId, state, exception, func) =>
            {
                logMessages.Add(state.ToString());
            });

        return logger;
    }

    private void PrintLogs(List<string> logMessages)
    {
        foreach (var log in logMessages)
        {
            _testOutputHelper.WriteLine(log);
        }
    }

}

public static class MockJobData
{
    public static readonly List<string> JobLocations = new List<string>
    {
        "New York, NY",
        "San Francisco, CA",
        "Austin, TX",
        "Seattle, WA",
        "Boston, MA"
    };

    public static readonly List<string> JobTitles = new List<string>
    {
        "Software Engineer",
        "Data Scientist",
        "Product Manager",
        "UX Designer",
        "DevOps Engineer"
    };

    public static readonly List<string> JobCompanies = new List<string>
    {
        "TechCorp",
        "DataSolutions",
        "Productify",
        "DesignWorld",
        "OpsHub"
    };

    public static readonly List<string> JobDescriptions = new List<string>
    {
        "Developing and maintaining software applications.",
        "Analyzing complex data sets to derive insights.",
        "Managing product lifecycle from ideation to release.",
        "Designing user-friendly interfaces.",
        "Maintaining and automating infrastructure operations."
    };

    public static readonly List<string> JobSalaries = new List<string>
    {
        "$120,000",
        "$110,000",
        "$130,000",
        "$100,000",
        "$115,000"
    };
}