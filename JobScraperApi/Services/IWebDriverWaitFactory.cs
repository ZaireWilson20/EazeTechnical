using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace EazeTechnical.Services;

public interface IWebDriverWaitFactory
{
    IWebDriverWait Create(IWebDriver driver, TimeSpan timeout);
}

public class WebDriverWaitFactory : IWebDriverWaitFactory
{
    public IWebDriverWait Create(IWebDriver driver, TimeSpan timeout)
    {
        var webDriverWait = new WebDriverWait(driver, timeout);
        return new WebDriverWaitWrapper(webDriverWait);
    }
}

public interface IWebDriverWait
{
    TResult Until<TResult>(Func<IWebDriver, TResult> condition);
}

public class WebDriverWaitWrapper : IWebDriverWait
{
    private readonly WebDriverWait _webDriverWait;

    public WebDriverWaitWrapper(WebDriverWait webDriverWait)
    {
        _webDriverWait = webDriverWait;
    }

    public TResult Until<TResult>(Func<IWebDriver, TResult> condition)
    {
        return _webDriverWait.Until(condition);
    }
}