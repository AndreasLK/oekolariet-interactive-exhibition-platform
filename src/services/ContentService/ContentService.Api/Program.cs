using System.Text.Json.Serialization;
using ContentService.Api.Common;
using ContentService.Api.Endpoints;
using ContentService.Api.ExceptionHandling;
using ContentService.Facade;
using ContentService.Infrastructure;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Keep HTTP endpoints decoupled from SQL Server and Application internals by wiring CQS Facades and Infrastructure adapters separately.
builder.Services.AddContentServiceFacade(configuration: builder.Configuration);
builder.Services.AddContentServiceInfrastructure(configuration: builder.Configuration);

// Force Minimal API parameter/body binding errors into GlobalExceptionHandler so clients still receive RFC 7807 ProblemDetails and CORS headers instead of bare 400 responses.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);

// Accept and emit readable enum names in JSON so exhibition schemas stay self-describing while preserving numeric compatibility.
builder.Services.ConfigureHttpJsonOptions(options =>
        options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Defer FormOptions and CorsOptions setup via IConfigureOptions so late host/test configuration overrides (such as WebApplicationFactory) take effect.
builder.Services.AddSingleton<IConfigureOptions<FormOptions>, ContentUploadFormOptionsSetup>();
builder.Services.AddSingleton<IConfigureOptions<CorsOptions>, BlazorCorsOptionsSetup>();

builder.Services.AddOpenApi();
builder.Services.AddCors();

WebApplication app = builder.Build();

// Register exception handling before CORS and endpoints so unhandled faults still pass through centralized ProblemDetails formatting.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
        // MapOpenApi() generates /openapi/v1.json, and UseSwaggerUI() serves the interactive Swagger UI page (/swagger) only in Development.
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
                options.SwaggerEndpoint(
                        url: ApiConstants.OPENAPI_DOCUMENT_ROUTE,
                        name: ApiConstants.SWAGGER_DOCUMENT_TITLE));
}

// Enforce the named Blazor CORS policy globally before route execution so preflight and endpoint responses share one origin rule.
app.UseCors(ApiConstants.BLAZOR_CORS_POLICY_NAME);

// Keep HTTP handlers thin and delegate all validation, quota, and persistence rules to Application use cases.
app.MapImageEndpoints();
app.MapTemplateEndpoints();
app.MapPublishingEndpoints();
app.MapHealthEndpoints();

app.Run();

/// <summary>
/// Exposes the top-level entry point type so <c>WebApplicationFactory&lt;Program&gt;</c> integration tests can boot the API host in-memory.
/// </summary>
public partial class Program
{
}
