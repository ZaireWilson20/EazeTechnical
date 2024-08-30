using System.Text.Json;
using EazeTechnical.Models;

namespace EazeTechnical.Middleware;

public class ValidateJsonPropertiesMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidateJsonPropertiesMiddleware> _logger;

    public ValidateJsonPropertiesMiddleware(RequestDelegate next, ILogger<ValidateJsonPropertiesMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Buffer the request body
        context.Request.EnableBuffering();

        var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();

        context.Request.Body.Position = 0;

        var allowedProperties = typeof(JobRequestDto).GetProperties()
            .Select(p => p.Name.ToLower())
            .ToHashSet();
        if (requestBody != "")
        {
            _logger.LogInformation(requestBody);
            using (JsonDocument document = JsonDocument.Parse(requestBody))
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (!allowedProperties.Contains(property.Name.ToLower()))
                    {
                        context.Response.StatusCode = StatusCodes.Status404NotFound;
                        await context.Response.WriteAsync($"Invalid parameter: {property.Name}");
                        return;
                    }
                }
            }
        }

        // Call the next delegate/middleware in the pipeline
        await _next(context);
    }
}