using EazeTechnical.Data;
using EazeTechnical.Middleware;
using EazeTechnical.Services;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);


var configuration = builder.Configuration;
builder.Services.AddDbContext<JobsDbContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("ScrapedJobPostingsDatabase")));
builder.Services.AddSingleton<IJobPostingScraper, JobScraper>();
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