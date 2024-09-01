using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace EazeTechnical.Services;

public interface IWebDriverFactory
{
    IWebDriver Create();
}

public class WebDriverFactory : IWebDriverFactory
{
    private readonly ChromeOptions _options;

    public WebDriverFactory(ChromeOptions options)
    {
        _options = options;
    }

    public IWebDriver Create()
    {
        return new ChromeDriver(_options);
    }
}



