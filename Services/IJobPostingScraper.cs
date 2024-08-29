using System.Net;
using EazeTechnical.Models;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;


namespace EazeTechnical.Services;

public interface IJobPostingScraper
{
    Task<IEnumerable<JobPostingDto>> ScrapeJobsAsync(string query, string location, int dateWithin, CancellationToken cancellationToken, int maxPageCount=100);
}

public class JobScraper : IJobPostingScraper
{
    private readonly IHttpClientFactory _httpClientFactory;
    private const string IndeedJobsUrl = "https://www.indeed.com/jobs";
    private const string IndeedViewJobUrl = "https://www.indeed.com/viewjob";
    private ILogger<IJobPostingScraper> _logger;
    public JobScraper(IHttpClientFactory httpClientFactory, ILogger<IJobPostingScraper> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        //MaxPageCount = maxPageCount;
    }
    
    public async Task<IEnumerable<JobPostingDto>> ScrapeJobsAsync(string? query, string? location, int dateWithin, CancellationToken cancellationToken, int maxPageCount=100)
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
            
                /*bool hasNextPage = true;
                IWebElement? nextPageElement = null;
            
                try
                {
                    wait.Until(findNextPage =>
                        findNextPage.FindElement(By.XPath("//a[contains(@data-testid, 'pagination-page-next')]")));
                    nextPageElement =
                        driver.FindElement(By.XPath("//a[contains(@data-testid, 'pagination-page-next')]"));
                    _logger.LogWarning("has nex page element - currently on page: {}", driver.Url);

                    hasNextPage = true;
                }
                catch (Exception e)
                {
                    hasNextPage = false;
                    _logger.LogWarning("Reading last page -- Exception: {}", e.Message);
                }*/
                
            driver.Navigate().GoToUrl(url);
        
            // Wait for the page to load and elements to be present
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(1.5));
            
            try
            {
                var allCards = wait.Until(card => card.FindElements(By.XPath("//div[contains(@data-testid, 'slider_container')]")));

                //var jobPosts = allCards.FindElement(By.XPath("//a[contains(@class, 'jcs-JobTitle')]"));

                await Task.Delay(TimeSpan.FromSeconds(7f));
                
                foreach (var jobPost in allCards)
                {
                
                    try
                    {
                        var jobLocation = jobPost.FindElement(By.XPath(".//div[contains(@data-testid, 'text-location')]")).Text;
                        var jobCompany = jobPost.FindElement(By.XPath(".//span[contains(@data-testid, 'company-name')]")).Text;
                        var jobTitle = jobPost.FindElement(By.XPath(".//a[contains(@class, 'jcs-JobTitle')]")).Text;
                        
                        bool withinDateRange = true;
                        if (dateWithin > 0)
                        {
                            var jobDays = jobPost.FindElement(By.XPath(".//span[contains(@data-testid, 'myJobsStateDate')]")).Text;
                            string days = new String(jobDays.Where(Char.IsDigit).ToArray());
                            if (days.Length > 0)
                            {
                                int val = int.Parse(days);
                                withinDateRange = val <= dateWithin;
                            }
                        }
                        
                        if(!withinDateRange) continue;
                        
                        if (!IsElementStale(jobPost))
                        {
                            jobPost.Click();
                        }
                        else
                        {
                            return jobPostings;
                        }

                        wait.Until(moreDetails =>
                            moreDetails.FindElement(By.XPath("//div[contains(@id, 'jobDescriptionText')]")));
                        
                        var closeDeets = driver.FindElement(
                            By.XPath("//button[contains(@class, 'jobsearch-ClosableViewJobPage-button-close')]"));
                        
                        var jobDescription = driver.FindElement(By.XPath("//div[contains(@id, 'jobDescriptionText')]")).Text;
                        var jobSalary = driver.FindElement(By.XPath("//div[contains(@id, 'salaryInfoAndJobType')]")).FindElement(By.XPath(".//span")).Text;
                        
                        
                        jobSalary = jobSalary.IndexOf("$", StringComparison.Ordinal) == 0 ? jobSalary : null;
                        closeDeets.Click();
                        wait.Until(closed =>
                            closed.FindElement(By.XPath(".//span[contains(@data-testid, 'myJobsStateDate')]")));
                    
                        jobPostings.Add(new JobPostingDto
                        {
                            Title = jobTitle,
                            Location = jobLocation,
                            Company = jobCompany,
                            Description = jobDescription,
                            Salary = jobSalary, 
                        });
                    }
                    catch (ElementClickInterceptedException ex)
                    {
                        _logger.LogWarning("Element Click Intercepted Exception: " + ex.Message);
                        break;
                    }
                    catch (StaleElementReferenceException ex)
                    {
                        _logger.LogWarning("Stale Element Reference Exception: " + ex.Message);
                        continue;
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning("Reading Cards -- Exception: {}", e.Message);
                        continue;
                    }
                    
                    const double maxWaitTime = 2.0f;
                    const double minWaitTime = .4f;
                    var randomWait = rand.NextDouble() * (maxWaitTime - minWaitTime) + minWaitTime;
                    await Task.Delay(TimeSpan.FromSeconds(randomWait), cancellationToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning("Searching Cards -- Exception: {}", e.Message);
                return jobPostings;
            }

                
                //TODO: Gotta find a way to get past the unclosable email popup
                /*if (hasNextPage)
                {
                    if (IsElementStale(nextPageElement))
                    {
                        return jobPostings;
                    }
                    //var nextUrl = nextPageElement?.GetAttribute("href");
                    _logger.LogWarning("pre going to Next Page {}", "Arrow");
                    nextPageElement?.Click();
                    //driver.Navigate().GoToUrl(nextUrl);
                    _logger.LogWarning("post going to Next Page");

                }*/
                
            driver.Quit();
        }

        return jobPostings;
    }

    bool IsElementStale(IWebElement? element)
    {
        if (element == null)
        {
            return true;
        }
        try
        {
            string tagName = element.TagName;
            return false;
        }
        catch
        {
            return true;
        }
    }
} 