using EazeTechnical.Data;
using EazeTechnical.Middleware;
using EazeTechnical.Services;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;


var builder = WebApplication.CreateBuilder(args);


var configuration = builder.Configuration;
builder.Services.AddDbContext<JobsDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("ScrapedJobPostingsDatabase")));

builder.Services.AddSingleton(serviceProvider =>
{
    var options = new ChromeOptions();
    options.AddArgument("--headless");
    options.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    options.AddArgument("--log-level=1");
    options.AddArgument("--disable-gpu");
    options.AddArgument("--no-sandbox");

    return options;
});

builder.Services.AddSingleton<IWebDriverFactory, WebDriverFactory>();
builder.Services.AddSingleton<IWebDriverWaitFactory, WebDriverWaitFactory>();

builder.Services.AddScoped<IJobPostingScraper, JobScraper>();
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage();

}

app.UseMiddleware<ValidateJsonPropertiesMiddleware>();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();