using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using EditorService.Api.ExceptionHandling;
using EditorService.Application.DependencyInjection;
using EditorService.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
string connectionString = builder.Configuration.GetConnectionString(name: "EditorDb")
        ?? throw new InvalidOperationException(message: "Connection string 'EditorDb' is missing.");

builder.Services
        .AddApplication()
        .AddInfrastructure(connectionString: connectionString);

builder.Services.AddControllers(configure: options =>
{
        // Keep "Async" in action names, so nameof(GetGameAsync) works in CreatedAtAction.
        options.SuppressAsyncSuffixInActionNames = false;
});

// API versioning: which version is used, and where it is read from.
builder.Services
        .AddApiVersioning(setupAction: options =>
        {
                // Version 1.0 is used when nothing else is specified.
                options.DefaultApiVersion = new ApiVersion(majorVersion: 1, minorVersion: 0);

                // Every response gets the header "api-supported-versions", e.g. "1.0".
                options.ReportApiVersions = true;

                // The version is read from the URL, e.g. "v1" in /api/v1/games.
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        // Makes [ApiVersion] and {version:apiVersion} work on the controllers.
        .AddMvc()
        // Lets OpenAPI and Swagger show the versions correctly.
        .AddApiExplorer(setupAction: options =>
        {
                // Groups the endpoints as "v1", "v2", ... based on [ApiVersion] on the controllers.
                options.GroupNameFormat = "'v'VVV";

                // Shows /api/v1/games in Swagger instead of /api/v{version}/games.
                options.SubstituteApiVersionInUrl = true;
        })
        // Creates one OpenAPI document per API version, e.g. /openapi/v1.json.
        // The versions are found automatically from [ApiVersion], so a new V2 controller
        // gets its own document without changing anything here.
        // Title and description come from <AssemblyTitle> and <Description> in EditorService.Api.csproj.
        .AddOpenApi(configureOptions: options =>
        {
                // Format of the documentation file (the OpenAPI standard), not the API version.
                options.Document.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
        });

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Health check: lets Docker ask "are you alive?" on /health.
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Answers 200 "Healthy" when the service is running.
app.MapHealthChecks(pattern: "/health");

// Swagger only in Development. Must come after MapControllers, so the API versions can be found.
if (app.Environment.IsDevelopment())
{
        // One OpenAPI document per API version: /openapi/v1.json, /openapi/v2.json, ...
        app.MapOpenApi().WithDocumentPerVersion();

        // Swagger UI at /swagger with one entry in the dropdown per API version.
        app.UseSwaggerUI(setupAction: options =>
        {
                // Text shown in the browser tab.
                options.DocumentTitle = "Editor Service API";

                foreach (ApiVersionDescription description in app.DescribeApiVersions())
                {
                        options.SwaggerEndpoint(url: $"/openapi/{description.GroupName}.json", name: $"Editor Service {description.GroupName}");
                }
        });
}

app.Run();
