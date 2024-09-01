using EazeTechnical.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;


namespace EazeTechnical.Services;

public interface IJobPostingScraper
{
    Task<Tuple<IEnumerable<JobPostingDto>, bool>> ScrapeJobsAsync(string query, string location, int? dateWithin, CancellationToken cancellationToken);
}

public class JobScraper : IJobPostingScraper
{
    private const string IndeedJobsUrl = "https://www.indeed.com/jobs";
    public const string CardContainerXPath = "//div[contains(@data-testid, 'slider_container')]";
    public const string CardJobLocationXPath = ".//div[contains(@data-testid, 'text-location')]";
    public const string CardJobCompanyXPath = ".//span[contains(@data-testid, 'company-name')]";
    public const string CardJobTitleXPath = ".//a[contains(@class, 'jcs-JobTitle')]";
    public const string CardJobTimePostedXPath = ".//span[contains(@data-testid, 'myJobsStateDate')]";
    public const string PageJobInfoComponentXPath = "//div[contains(@class, 'jobsearch-JobComponent')]";
    public const string PageJobDescriptionXPath = "//div[contains(@id, 'jobDescriptionText')]" ;
    public const string PageSalaryContainerXPath = "//div[contains(@id, 'salaryInfoAndJobType')]" ;

    public List<Tuple<string, string>> XPathWarningExceptions; 
    private readonly ILogger<IJobPostingScraper> _logger;
    private readonly IWebDriverFactory _webDriverFactory;
    private readonly IWebDriverWaitFactory _webDriverWaitFactory;

    public bool ScrapJobsTestFlag = false;
    public JobScraper(ILogger<IJobPostingScraper> logger, IWebDriverFactory webDriverFactory, IWebDriverWaitFactory webDriverWaitFactory)
    {
        XPathWarningExceptions = new List<Tuple<string, string>>();
        _logger = logger;
        _webDriverFactory = webDriverFactory;
        _webDriverWaitFactory = webDriverWaitFactory;
    }
    
    public async Task<Tuple<IEnumerable<JobPostingDto>, bool>> ScrapeJobsAsync(string? query, string? location, int? dateWithin, CancellationToken cancellationToken)
    {
        var jobPostings = new List<JobPostingDto>();
        var paramsString = $"?q={query}&l={location}";
        var url = IndeedJobsUrl + paramsString;
        
        
        var rand = new Random();
        using (var driver = _webDriverFactory.Create())
        {

            driver.Navigate().GoToUrl(url);
            
            var wait = _webDriverWaitFactory.Create(driver, TimeSpan.FromSeconds(10));
            int cardCount = 0;
            try
            {
                // Wait til card containers are found
                var allCards = wait.Until(card => card.FindElements(By.XPath(CardContainerXPath)));
                cardCount = allCards.Count;
                int currentCard = 1;
                _logger.LogInformation("Scraping Started, opened web driver. Initial Job Card Count: {} -- Job Cards Scraped {}", cardCount, jobPostings.Count);

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
                            _logger.LogWarning("Card {}, attempt {}", currentCard, currentAttempt + 1);
                        }
                        shouldRetry = false;
                        currentAttempt++;
                        try
                        {
                            // Look for job loc
                            string? jobLocation = null;
                            try
                            {
                                jobLocation = jobPost
                                    .FindElement(By.XPath(CardJobLocationXPath)).Text;
                            }
                            catch (Exception ex)
                            {
                                XPathWarningExceptions.Add(new Tuple<string, string>(CardJobLocationXPath, ex.Message));
                            }
                            
                            // Look for job company
                            string? jobCompany = null;
                            try
                            {
                                jobCompany = jobPost.FindElement(By.XPath(CardJobCompanyXPath)).Text;
                            }
                            catch (Exception ex)
                            {
                                XPathWarningExceptions.Add(new Tuple<string, string>(CardJobCompanyXPath, ex.Message));
                            }

                            // Look for job Title
                            string? jobTitle = null;
                            try
                            {
                                jobTitle = jobPost.FindElement(By.XPath(CardJobTitleXPath)).Text;
                            }
                            catch (Exception ex)
                            {
                                XPathWarningExceptions.Add(new Tuple<string, string>(CardJobTitleXPath, ex.Message));
                            }
                            
                            bool withinDateRange = true;
                            if (dateWithin > 0)
                            {
                                try
                                {
                                    var jobDays = jobPost.FindElement(By.XPath(CardJobTimePostedXPath)).Text;
                                    string days = new String(jobDays.Where(Char.IsDigit).ToArray());
                                    if (days.Length > 0)
                                    {
                                        int val = int.Parse(days);
                                        withinDateRange = val <= dateWithin;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    XPathWarningExceptions.Add(new Tuple<string, string>(CardJobTimePostedXPath, ex.Message));
                                }

                            }
                            
                            if(!withinDateRange) continue;
                            

                            jobPost.Click();


                            try
                            {
                                // Waiting til full details window is up
                                wait.Until(moreDetails =>
                                    moreDetails.FindElement(By.XPath(PageJobInfoComponentXPath)));
                            }
                            catch (Exception ex)
                            {
                                XPathWarningExceptions.Add(new Tuple<string, string>(PageJobInfoComponentXPath, ex.Message));
                                continue;
                            }
         
                            string? jobDescription = null;
                            try
                            {
                                jobDescription = driver
                                    .FindElement(By.XPath(PageJobDescriptionXPath)).Text;
                            }
                            catch (StaleElementReferenceException)
                            {
                                _logger.LogWarning("Finding Job Description -- Stale Reference Exception, Retying Card");
                                shouldRetry = true;
                                continue;
                            }
                            catch (Exception ex)
                            {
                                jobDescription = null;
                                XPathWarningExceptions.Add(new Tuple<string, string>(PageJobDescriptionXPath, ex.Message));
                            }

                            string? jobSalary = null;
                            try
                            {
                                jobSalary = driver.FindElement(By.XPath(PageSalaryContainerXPath)).FindElement(By.XPath(".//span")).Text;
                                jobSalary = jobSalary.IndexOf("$", StringComparison.Ordinal) == 0 ? jobSalary : null;
                            }
                            catch (Exception ex)
                            {
                                jobSalary = null;
                                XPathWarningExceptions.Add(new Tuple<string, string>(PageSalaryContainerXPath, ex.Message));
                            }
                            
                            
                            AddToJobList(ref jobPostings, jobTitle, jobLocation, jobCompany, jobDescription, jobSalary);
                        }
                        // Most likely due to popup that cannot be close -- just return what has been collected so far
                        catch (ElementClickInterceptedException ex)
                        {
                            _logger.LogError("Element Click Intercepted Exception: {}\nStack Trace:\n{}", ex.Message, ex.StackTrace);
                            _logger.LogInformation("Scraping finished, closing web driver. Initial Job Card Count: {} -- Job Cards Scraped {}", cardCount, jobPostings.Count);
                            return new Tuple<IEnumerable<JobPostingDto>, bool>(jobPostings, false);
                        }
                        catch (StaleElementReferenceException ex)
                        {
                            _logger.LogWarning("Stale Element Reference Exception: " + ex.Message);
                            shouldRetry = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Reading Cards -- Exception: {}", ex.Message);
                            shouldRetry = true;
                        }
                        

                    }

                    if (currentAttempt == maxRetries && shouldRetry)
                    {
                        _logger.LogWarning("Cannot add Card {} to list, reached max attempts", currentCard);
                    }
                    await RandomDelayTask(rand, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Searching Cards -- Exception: {}\nStack Trace:\n{}", ex.Message, ex.StackTrace);
                _logger.LogInformation("Scraping finished, closing web driver. Initial Job Card Count: {} -- Job Cards Scraped {}", cardCount, jobPostings.Count);
                return new Tuple<IEnumerable<JobPostingDto>, bool>(jobPostings, false);
            }
            
            
            _logger.LogInformation("Scraping finished, closing web driver. Initial Job Card Count: {} -- Job Cards Scraped {}", cardCount, jobPostings.Count);
            driver.Quit();
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
