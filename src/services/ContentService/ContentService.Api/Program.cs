using System.Text.Json.Serialization;
using Asp.Versioning;
using Asp.Versioning.Builder;
using ContentService.Api.Common;
using ContentService.Api.Endpoints;
using ContentService.Api.ExceptionHandling;
using ContentService.Facade;
using ContentService.Infrastructure;
using ContentService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Keep HTTP endpoints decoupled from SQL Server and Application internals by wiring CQS Facades and Infrastructure adapters separately.
builder.Services.AddContentServiceFacade(configuration: builder.Configuration);
builder.Services.AddContentServiceInfrastructure(configuration: builder.Configuration);

// Expose a lightweight built-in liveness/readiness endpoint (/healthz) for Dapr sidecars and Docker orchestrators without extra packages.
builder.Services.AddHealthChecks();

// Configure URL-segment API versioning (/api/v1/...) with explicit version requirement so breaking changes can be introduced cleanly in v2+.
builder.Services.AddApiVersioning(options =>
{
        options.DefaultApiVersion = new ApiVersion(
                majorVersion: ApiConstants.API_VERSION_MAJOR,
                minorVersion: ApiConstants.API_VERSION_MINOR);
        // Require explicit /api/v{version}/... segments on versioned routes so unversioned drift is caught immediately.
        options.AssumeDefaultVersionWhenUnspecified = false;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
})
.AddApiExplorer(options =>
{
        options.GroupNameFormat = ApiConstants.API_EXPLORER_GROUP_NAME_FORMAT;
        // Substitute {version:apiVersion} with concrete v1 in generated OpenAPI paths so Swagger UI issues valid requests.
        options.SubstituteApiVersionInUrl = true;
})
.AddOpenApi();

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

builder.Services.AddCors();

WebApplication app = builder.Build();

// Apply EF Core migrations on startup when explicitly enabled (e.g., Docker Compose multi-node containers).
if (app.Configuration.GetValue<bool>("Database:AutoMigrateOnStartup"))
{
        using IServiceScope migrationScope = app.Services.CreateScope();
        ContentDbContext dbContext = migrationScope.ServiceProvider.GetRequiredService<ContentDbContext>();
        await dbContext.Database.MigrateAsync();
}

// Register exception handling before CORS and endpoints so unhandled faults still pass through centralized ProblemDetails formatting.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
        // MapOpenApi().WithDocumentPerVersion() generates /openapi/v1.json, and UseSwaggerUI() serves /swagger only in Development.
        app.MapOpenApi().WithDocumentPerVersion();
        app.UseSwaggerUI(options =>
                options.SwaggerEndpoint(
                        url: ApiConstants.OPENAPI_DOCUMENT_ROUTE,
                        name: ApiConstants.SWAGGER_DOCUMENT_TITLE));
}

// Enforce the named Blazor CORS policy globally before route execution so preflight and endpoint responses share one origin rule.
app.UseCors(ApiConstants.BLAZOR_CORS_POLICY_NAME);

// Map /healthz and /dapr/subscribe outside versioned API groups so Dapr and container probes remain version-neutral.
app.MapHealthChecks(pattern: ApiConstants.LIVENESS_HEALTH_ROUTE);
app.MapDaprSubscriptions(configuration: app.Configuration);

// Build a single versioned API group (/api/v{version:apiVersion}) shared across all ContentService endpoint modules.
ApiVersionSet apiVersionSet = app.NewApiVersionSet()
        .HasApiVersion(new ApiVersion(
                majorVersion: ApiConstants.API_VERSION_MAJOR,
                minorVersion: ApiConstants.API_VERSION_MINOR))
        .ReportApiVersions()
        .Build();

RouteGroupBuilder versionedApiGroup = app.MapGroup(prefix: ApiConstants.VERSIONED_API_ROUTE_PREFIX)
        .WithApiVersionSet(apiVersionSet: apiVersionSet);

// Keep HTTP handlers thin and delegate all validation, quota, and persistence rules to Application use cases.
versionedApiGroup.MapImageEndpoints();
versionedApiGroup.MapTemplateEndpoints();
versionedApiGroup.MapPublishingEndpoints();
versionedApiGroup.MapHealthEndpoints();
versionedApiGroup.MapPoolEndpoints();

app.Run();

/// <summary>
/// Exposes the top-level entry point type so <c>WebApplicationFactory&lt;Program&gt;</c> integration tests can boot the API host in-memory.
/// </summary>
public partial class Program
{
}
