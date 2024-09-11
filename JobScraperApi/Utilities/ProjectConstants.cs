namespace EazeTechnical.Utilities;

public static class ProjectConstants
{
    public struct Urls
    {
        public const string IndeedJobsUrl = "https://www.indeed.com/jobs";
    }

    public struct Xpaths
    {
        public const string CardContainerXPath = "//div[contains(@data-testid, 'slider_container')]";
        public const string CardJobLocationXPath = ".//div[contains(@data-testid, 'text-location')]";
        public const string CardJobCompanyXPath = ".//span[contains(@data-testid, 'company-name')]";
        public const string CardJobTitleXPath = ".//a[contains(@class, 'jcs-JobTitle')]";
        public const string CardJobTimePostedXPath = ".//span[contains(@data-testid, 'myJobsStateDate')]";
        public const string PageJobInfoComponentXPath = "//div[contains(@class, 'jobsearch-JobComponent')]";
        public const string PageJobDescriptionXPath = "//div[contains(@id, 'jobDescriptionText')]";
        public const string PageSalaryContainerXPath = "//div[contains(@id, 'salaryInfoAndJobType')]";
        public const string LocalSpanPath = ".//span";
    }

    public struct ResponseErrorMessages
    {
        
        public const string DatabaseNoContext500 = "Database context not available.";
        public const string ScrapeRequestTimeout408 = "The scraping operation timed out.";
        public const string GenericInternalError500 = "An error occurred";
        public const string SeleniumClickInterceptException = "Element Click Intercepted Exception";
        public const string QueryIdNotInDatabase404 = "Query ID not found in database.";
    }
    
    public struct ResponseWarningMessages
    {
        public const string MissingJobsFindWarningResponse200 = "Some job posts may be missing due to an exception hit while scraping.";
        public const string SeleniumStaleElementException = "Stale Element Reference Exception";
        public const string FindByIdWarningResponse200 = "The id provided does not exist.";
    }
    
    public struct ResponseSuccessMessages
    {
        public const string FindSuccessResponse200 = "Full page scraped.";
        public const string FindByIdSuccessResponse200 = "Query Retrieved";
    }

    public struct ExceptionTypeStrings
    {
        public const string InvalidFactoryArguments = "Invalid Factory Arguments Exception";
    }    
    
    public struct ExceptionArgumentMessages
    {
        public const string InvalidFactoryArgument = "Invalid arguments for creating: ";
    }
    
    public struct JobRequestDefaultFields
    {
        public const string Query = "Cannabis";
        public const string Location = "California";
        public const int LastNdays = -1;
    }

    public struct GeneralUseString
    {
        public const string EmptyJsonObj = "{}";
        public const string WebDriverClass = "WebDriver";
        public const string WebDriverWaitClass = "WebDriverWait";
    }
    
    
    
    

}