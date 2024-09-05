using System.Text.Json;
using System.Text.Json.Serialization;
using EazeTechnical.Data;
using EazeTechnical.Middleware;
using EazeTechnical.Services;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;


var builder = WebApplication.CreateBuilder(args);


var configuration = builder.Configuration;
builder.Services.AddDbContext<JobsDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("ScrapedJobPostingsDatabase")));

var chromeOptions = new ChromeOptions();
chromeOptions.AddArgument("--headless");
chromeOptions.AddArgument("user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
chromeOptions.AddArgument("--log-level=1");
chromeOptions.AddArgument("--disable-gpu");
chromeOptions.AddArgument("--no-sandbox");


var jsonSerializerOptions = new JsonSerializerOptions
{
    ReferenceHandler = ReferenceHandler.IgnoreCycles,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};

builder.Services.AddSingleton(chromeOptions);
builder.Services.AddSingleton(jsonSerializerOptions);

builder.Services.AddSingleton<IFactory<IWebDriver>, WebDriverFactory>();
builder.Services.AddSingleton<IFactory<IWebDriverWait>, WebDriverWaitFactory>();

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