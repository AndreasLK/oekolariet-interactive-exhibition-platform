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

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi

builder.Services.AddOpenApi(documentName: "v1", configureOptions: options =>
{
        // Use OpenAPI 3.0, as in the course material.
        options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;

        // A document transformer can change the OpenAPI document before it is sent.
        // Here it sets the information shown at the top of Swagger UI.
        //Service version is read from the .csproj file, e.g. "0.1.0".
        options.AddDocumentTransformer(transformer: (document, context, cancellationToken) =>
        {
                document.Info = new()
                {
                        Title = "Editor Service API",
                        Version = "v1",
                        Description = $"Create and edit sorting games and quizzes. Service version {ServiceVersion()}."
                };

                return Task.CompletedTask;
        });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
        // The OpenAPI document: /openapi/v1.json
        app.MapOpenApi();

        // Interactive documentation that reads the document above: /swagger
        app.UseSwaggerUI(setupAction: options =>
        {
                options.SwaggerEndpoint(url: "/openapi/v1.json", name: "v1");
        });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

// Reads <Version> from the .csproj, e.g. "0.1.0".
static string ServiceVersion() => typeof(Program).Assembly.GetName().Version?.ToString(fieldCount: 3) ?? "unknown";
