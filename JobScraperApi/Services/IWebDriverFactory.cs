using EazeTechnical.Utilities;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace EazeTechnical.Services;


public class WebDriverFactory : IFactory<IWebDriver>
{
    
    /// <summary>
    /// Creates a new instance of IWebDriver.
    /// </summary>
    /// <param name="args">Expected arguments: ChromeOptions options.</param>
    /// <returns>A configured IWebDriver instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the arguments are invalid.</exception>
    public IWebDriver Create(params object[] args)
    {
        if (args.Length != 1 || args[0] is not ChromeOptions options)
        {
            throw new InvalidFactoryArgumentsException(ProjectConstants.ExceptionArgumentMessages.InvalidFactoryArgument + ProjectConstants.GeneralUseString.WebDriverClass);
        }

        return new ChromeDriver(options);
    }
}



