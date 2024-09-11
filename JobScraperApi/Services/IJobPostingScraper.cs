using EazeTechnical.Models;
using EazeTechnical.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace EazeTechnical.Services;

public interface IJobPostingScraper
{
    Task<Tuple<IEnumerable<JobPostingDto>, bool>> ScrapeJobsAsync(string query, string location, int? dateWithin, CancellationToken cancellationToken);
}

public class JobScraper : IJobPostingScraper
{
    private readonly ILogger<IJobPostingScraper> _logger;
    private readonly IFactory<IWebDriver> _webDriverFactory;
    private readonly IFactory<IWebDriverWait> _webDriverWaitFactory;
    private readonly ChromeOptions _chromeOptions;
    public JobScraper(ILogger<IJobPostingScraper> logger, IFactory<IWebDriver> webDriverFactory, IFactory<IWebDriverWait> webDriverWaitFactory, ChromeOptions chromeOptions)
    {
        _logger = logger;
        _webDriverFactory = webDriverFactory;
        _webDriverWaitFactory = webDriverWaitFactory;
        _chromeOptions = chromeOptions;
    }

    public async Task<Tuple<IEnumerable<JobPostingDto>, bool>> ScrapeJobsAsync(string? query, string? location, int? dateWithin, CancellationToken cancellationToken)
    {
        var jobPostings = new List<JobPostingDto>();
        var paramsString = $"?q={query}&l={location}";
        var url = ProjectConstants.Urls.IndeedJobsUrl + paramsString;

        var rand = new Random();

        try
        {
            using (var driver = _webDriverFactory.Create(_chromeOptions))
            {
                driver.Navigate().GoToUrl(url);

                int cardCount = 0;
                try
                {
                    var wait = _webDriverWaitFactory.Create(driver, TimeSpan.FromSeconds(10));
                    var allCards = wait.Until(card =>
                        card.FindElements(By.XPath(ProjectConstants.Xpaths.CardContainerXPath)));
                    cardCount = allCards.Count;
                    int currentCard = 1;
                    _logger.LogInformation(
                        "Scraping Started, opened web driver. Initial Job Card Count: {CardCount} -- Job Cards Scraped {JobCardsScraped}",
                        cardCount, jobPostings.Count);

                    foreach (var jobPost in allCards)
                    {
                        bool shouldRetry = true;
                        int maxRetries = 3;
                        int currentAttempt = 0;
                        currentCard++;
                        while (shouldRetry && currentAttempt < maxRetries)
                        {
                            if (currentAttempt > 0)
                            {
                                _logger.LogWarning("Card {CurrentCard}, attempt {Attempt}", currentCard,
                                    currentAttempt + 1);
                            }

                            shouldRetry = false;
                            currentAttempt++;
                            try
                            {
                                string? jobLocation = null;
                                try
                                {
                                    jobLocation = jobPost
                                        .FindElement(By.XPath(ProjectConstants.Xpaths.CardJobLocationXPath)).Text;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Failed to find job location: {Message}", ex.Message);
                                }

                                string? jobCompany = null;
                                try
                                {
                                    jobCompany = jobPost
                                        .FindElement(By.XPath(ProjectConstants.Xpaths.CardJobCompanyXPath)).Text;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Failed to find job company: {Message}", ex.Message);
                                }

                                string? jobTitle = null;
                                try
                                {
                                    jobTitle = jobPost.FindElement(By.XPath(ProjectConstants.Xpaths.CardJobTitleXPath))
                                        .Text;
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Failed to find job title: {Message}", ex.Message);
                                }

                                bool withinDateRange = true;
                                if (dateWithin > 0)
                                {
                                    try
                                    {
                                        var jobDays = jobPost
                                            .FindElement(By.XPath(ProjectConstants.Xpaths.CardJobTimePostedXPath)).Text;
                                        string days = new String(jobDays.Where(Char.IsDigit).ToArray());
                                        if (days.Length > 0)
                                        {
                                            int val = int.Parse(days);
                                            withinDateRange = val <= dateWithin;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning("Failed to find job time posted: {Message}", ex.Message);
                                    }
                                }

                                if (!withinDateRange) continue;

                                jobPost.Click();

                                try
                                {
                                    // Waiting til full details window is up
                                    wait.Until(moreDetails =>
                                        moreDetails.FindElement(By.XPath(ProjectConstants.Xpaths
                                            .PageJobInfoComponentXPath)));
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Failed to find job info component: {Message}", ex.Message);
                                    continue;
                                }

                                string? jobDescription = null;
                                try
                                {
                                    jobDescription = driver
                                        .FindElement(By.XPath(ProjectConstants.Xpaths.PageJobDescriptionXPath)).Text;
                                }
                                catch (StaleElementReferenceException)
                                {
                                    _logger.LogWarning(
                                        "Finding Job Description -- Stale Reference Exception, Retying Card");
                                    shouldRetry = true;
                                    continue;
                                }
                                catch (Exception ex)
                                {
                                    jobDescription = null;
                                    _logger.LogWarning("Failed to find job description: {Message}", ex.Message);
                                }

                                string? jobSalary = null;
                                try
                                {
                                    jobSalary = driver
                                        .FindElement(By.XPath(ProjectConstants.Xpaths.PageSalaryContainerXPath))
                                        .FindElement(By.XPath(ProjectConstants.Xpaths.LocalSpanPath)).Text;
                                    jobSalary = jobSalary.IndexOf("$", StringComparison.Ordinal) == 0
                                        ? jobSalary
                                        : null;
                                }
                                catch (Exception ex)
                                {
                                    jobSalary = null;
                                    _logger.LogWarning("Failed to find job salary: {Message}", ex.Message);
                                }

                                AddToJobList(ref jobPostings, jobTitle, jobLocation, jobCompany, jobDescription,
                                    jobSalary);
                            }
                            // Most likely due to popup that cannot be close -- just return what has been collected so far
                            catch (ElementClickInterceptedException ex)
                            {
                                _logger.LogError("{ExceptionType} : {ExceptionMessage}\nStack Trace:\n{StackTrace}",
                                    ProjectConstants.ResponseErrorMessages.SeleniumClickInterceptException, ex.Message,
                                    ex.StackTrace);

                                _logger.LogInformation(
                                    "Scraping finished, closing web driver. Initial Job Card Count: {CardCount} -- Job Cards Scraped {JobCardsScraped}",
                                    cardCount, jobPostings.Count);

                                return new Tuple<IEnumerable<JobPostingDto>, bool>(jobPostings, false);
                            }
                            catch (StaleElementReferenceException ex)
                            {
                                _logger.LogWarning("{WarningType}: {WarningMessage}",
                                    ProjectConstants.ResponseWarningMessages.SeleniumStaleElementException, ex.Message);
                                shouldRetry = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Reading Cards -- Exception: {ExceptionMessage}", ex.Message);
                                shouldRetry = true;
                            }
                        }

                        if (currentAttempt == maxRetries && shouldRetry)
                        {
                            _logger.LogWarning("Cannot add Card {CurrentCard} to list, reached max attempts",
                                currentCard);
                        }

                        await RandomDelayTask(rand, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("Searching Cards -- Exception: {ExceptionMessage}\nStack Trace:\n{StackTrace}",
                        ex.Message, ex.StackTrace);
                    _logger.LogInformation(
                        "Scraping finished, closing web driver. Initial Job Card Count: {CardCount} -- Job Cards Scraped {JobCardsScraped}",
                        cardCount, jobPostings.Count);
                    return new Tuple<IEnumerable<JobPostingDto>, bool>(jobPostings, false);
                }

                _logger.LogInformation(
                    "Scraping finished, closing web driver. Initial Job Card Count: {CardCount} -- Job Cards Scraped {JobCardsScraped}",
                    cardCount, jobPostings.Count);
                driver.Quit();
            }
        }
        catch (InvalidFactoryArgumentsException ex)
        {
            _logger.LogError("{ExceptionType}: {ExceptionMessage}", ProjectConstants.ExceptionTypeStrings.InvalidFactoryArguments, ex.Message);
        }

        return new Tuple<IEnumerable<JobPostingDto>, bool>(jobPostings, true);
    }

    private Task RandomDelayTask(Random rand, CancellationToken cancellationToken)
    {
        const double maxWaitTime = .3f;
        const double minWaitTime = .1f;
        var randomWait = rand.NextDouble() * (maxWaitTime - minWaitTime) + minWaitTime;
        return Task.Delay(TimeSpan.FromSeconds(randomWait), cancellationToken);
    }

    private void AddToJobList(ref List<JobPostingDto> jobPostings, string? title, string? location, string? company, string? description, string? salary)
    {
        jobPostings.Add(new JobPostingDto
        {
            Title = title,
            Location = location,
            Company = company,
            Description = description,
            Salary = salary,
        });
    }
}