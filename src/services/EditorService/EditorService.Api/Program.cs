using Asp.Versioning;
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

                // A call without a version gets the default version instead of an error.
                options.AssumeDefaultVersionWhenUnspecified = true;

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
                // Groups the endpoints as "v1", "v2", ... These names must match the names in apiVersions.
                options.GroupNameFormat = "'v'VVV";

                // Shows /api/v1/games in Swagger instead of /api/v{version}/games.
                options.SubstituteApiVersionInUrl = true;
        });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

// All API versions the service exposes.
// Add "v2" here when a controller with [ApiVersion(version: 2.0)] exists.
// Each version gets its own document in Swagger, so v1 and v2 can be tested side by side. through swagger by typing ["v1", "v2"]
string[] apiVersions = ["v1"];

// Create one OpenAPI document per API version, e.g. /openapi/v1.json.
// Each document only contains the endpoints in its version group ("v1"),
// which comes from GroupNameFormat in AddApiExplorer.
foreach (string apiVersion in apiVersions)
{
        builder.Services.AddOpenApi(documentName: apiVersion, configureOptions: options =>
        {
                // Format of the documentation file (the OpenAPI standard), not the API version.
                options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;

                // Runs every time the document is requested and fills in the header shown in Swagger UI.
                options.AddDocumentTransformer(transformer: (document, context, cancellationToken) =>
                {
                        document.Info = new()
                        {
                                Title = "Editor Service API",
                                // The name of the document being built, e.g. "v1". Never hardcoded.
                                Version = context.DocumentName,
                                // Service version is read from <Version> in EditorService.Api.csproj.
                                Description = $"Create and edit sorting games and quizzes. Service version {ServiceVersion()}."
                        };

                        // Nothing async happens here, so the transformer finishes right away.
                        return Task.CompletedTask;
                });
        });
}

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
        // The OpenAPI document: /openapi/v1.json
        app.MapOpenApi();

        // Swagger UI at /swagger. Reads the OpenAPI documents made above.
        app.UseSwaggerUI(setupAction: options =>
        {
                // Text shown in the browser tab.
                options.DocumentTitle = "Editor Service API";

                // One entry in the Swagger dropdown per API version.
                foreach (string apiVersion in apiVersions)
                {
                        options.SwaggerEndpoint(url: $"/openapi/{apiVersion}.json", name: $"Editor Service {apiVersion}");
                }
        });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Reads <Version> from the .csproj, e.g. "0.1.0".
static string ServiceVersion() => typeof(Program).Assembly.GetName().Version?.ToString(fieldCount: 3) ?? "unknown";
