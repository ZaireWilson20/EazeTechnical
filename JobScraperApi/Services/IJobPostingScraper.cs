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
    private ILogger<IJobPostingScraper> _logger;
    public JobScraper(ILogger<IJobPostingScraper> logger)
    {
        _logger = logger;
    }
    
    public async Task<Tuple<IEnumerable<JobPostingDto>, bool>> ScrapeJobsAsync(string? query, string? location, int? dateWithin, CancellationToken cancellationToken)
    {
        var jobPostings = new List<JobPostingDto>();
        var paramsString = $"?q={query}&l={location}";
        var url = IndeedJobsUrl + paramsString;
        
        var options = new ChromeOptions();
        options.AddArgument("--headless"); 
        options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        options.AddArgument("--log-level=1");
        options.AddArgument("--disable-gpu");
        options.AddArgument("--no-sandbox");
        
        var rand = new Random();
        using (var driver = new ChromeDriver(options))
        {

            driver.Navigate().GoToUrl(url);
            
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            int cardCount = 0;
            try
            {
                // Wait til card containers are found
                var allCards = wait.Until(card => card.FindElements(By.XPath("//div[contains(@data-testid, 'slider_container')]")));
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
                                    .FindElement(By.XPath(".//div[contains(@data-testid, 'text-location')]")).Text;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Finding Job Location -- Exception: {}", ex.Message);
                            }
                            
                            // Look for job company
                            string? jobCompany = null;
                            try
                            {
                                jobCompany = jobPost.FindElement(By.XPath(".//span[contains(@data-testid, 'company-name')]")).Text;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Finding Job Company -- Exception: {}", ex.Message);
                            }

                            // Look for job Title
                            string? jobTitle = null;
                            try
                            {
                                jobTitle = jobPost.FindElement(By.XPath(".//a[contains(@class, 'jcs-JobTitle')]")).Text;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Finding Job Title -- Exception: {}", ex.Message);
                            }
                            
                            bool withinDateRange = true;
                            if (dateWithin > 0)
                            {
                                try
                                {
                                    var jobDays = jobPost.FindElement(By.XPath(".//span[contains(@data-testid, 'myJobsStateDate')]")).Text;
                                    string days = new String(jobDays.Where(Char.IsDigit).ToArray());
                                    if (days.Length > 0)
                                    {
                                        int val = int.Parse(days);
                                        withinDateRange = val <= dateWithin;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning("Looking For Job Day Post/Active -- Exception: {}", ex.Message);
                                }

                            }
                            
                            if(!withinDateRange) continue;
                            

                            jobPost.Click();


                            try
                            {
                                // Waiting til full details window is up
                                wait.Until(moreDetails =>
                                    moreDetails.FindElement(By.XPath("//div[contains(@class, 'jobsearch-JobComponent')]")));
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning("Waiting for Full Details Element -- Exception: {}", ex.Message);
                                continue;
                            }
         
                            string? jobDescription = null;
                            try
                            {
                                jobDescription = driver
                                    .FindElement(By.XPath("//div[contains(@id, 'jobDescriptionText')]")).Text;
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
                                _logger.LogWarning("Finding Job Description -- Exception: {}", ex.Message);
                            }

                            string? jobSalary = null;
                            try
                            {
                                jobSalary = driver.FindElement(By.XPath("//div[contains(@id, 'salaryInfoAndJobType')]")).FindElement(By.XPath(".//span")).Text;
                                jobSalary = jobSalary.IndexOf("$", StringComparison.Ordinal) == 0 ? jobSalary : null;
                            }
                            catch (Exception ex)
                            {
                                jobSalary = null;
                                _logger.LogWarning("Finding Job Salary -- Exception: {}", ex.Message);
                            }
                            
                            
                            AddToJobList(ref jobPostings, jobTitle, jobLocation, jobCompany, jobDescription, jobSalary);
                        }
                        // Most likely due to popup that cannot be close -- just return what has been collected so far
                        catch (ElementClickInterceptedException ex)
                        {
                            _logger.LogWarning("Element Click Intercepted Exception: " + ex.Message);
                            _logger.LogInformation("Scraping finished, closing web driver. Initial Job Card Count: {} -- Job Cards Scraped {}", cardCount, jobPostings.Count);
                            return new Tuple<IEnumerable<JobPostingDto>, bool>(jobPostings, false);
                        }
                        catch (StaleElementReferenceException ex)
                        {
                            _logger.LogWarning("Stale Element Reference Exception: " + ex.Message);
                            shouldRetry = true;
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning("Reading Cards -- Exception: {}", e.Message);
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
                _logger.LogWarning("Searching Cards -- Exception: {}", ex.Message);
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
