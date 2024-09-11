using EazeTechnical.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace EazeTechnical.Services;

public class WebDriverWaitFactory : IFactory<IWebDriverWait>
{
    /// <summary>
    /// Creates a new instance of IWebDriverWait.
    /// </summary>
    /// <param name="args">Expected arguments: IWebDriver driver, TimeSpan timeout.</param>
    /// <returns>A configured IWebDriverWait instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the arguments are invalid.</exception>
    public IWebDriverWait Create(params object[] args)
    {
        if (args.Length != 2 || args[0] is not IWebDriver driver || args[1] is not TimeSpan timeout)
        {
            throw new InvalidFactoryArgumentsException(ProjectConstants.ExceptionArgumentMessages.InvalidFactoryArgument + ProjectConstants.GeneralUseString.WebDriverWaitClass);
        }
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