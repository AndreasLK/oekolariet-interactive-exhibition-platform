using HighscoreService.Application.DependencyInjection;
using HighscoreService.Hub.Api.Authentication;
using HighscoreService.Hub.Api.Endpoints.Highscores;
using HighscoreService.Hub.Api.Endpoints.Screens;
using HighscoreService.Hub.Api.Endpoints.Statistics;
using HighscoreService.Hub.Api.Endpoints.Sync;
using HighscoreService.Hub.Api.ExceptionHandling;
using HighscoreService.Hub.Api.Extensions;
using HighscoreService.Hub.Infrastructure;
using HighscoreService.Hub.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// HighscoreService.Hub.Api is the central ("big one") SQL Server authority in the exhibition topology:
// - Public leaderboards (GET/POST /api/v1/highscores) are open anonymously for displays and web visitors.
// - Score submission (POST /api/v1/highscores) accepts either approved Screen servers (scr_ tokens) or AuthService users (JWT).
// - Sync endpoints (/api/v1/highscores/sync/*) require an approved Screen token (or Hub Admin).
// - Admin operations (delete scores, view analytics, approve/revoke screens) require an AuthService JWT.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddHubReverseProxyAndCors(builder.Configuration);
builder.Services.AddHubAuthenticationAndAuthorization(builder.Configuration);

builder.Services.AddHighscoreHubApplication();
builder.Services.AddHighscoreHubInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
        // NON-STANDARD DECISION: EnsureCreatedAsync() is used instead of EF Core Migrations,
        // with a bounded retry loop to tolerate cold SQL Server container startup races.
        // Pros: Zero-touch schema provisioning on fresh exhibition databases without a separate migration runner.
        // Cons: Schema evolution on existing databases requires explicit migration scripts or manual DDL.
        HubHighscoreDbContext dbContext = scope.ServiceProvider.GetRequiredService<HubHighscoreDbContext>();
        const int MAX_ATTEMPTS = 5;
        for (int attempt = 1; attempt <= MAX_ATTEMPTS; attempt++)
        {
                try
                {
                        await dbContext.Database.EnsureCreatedAsync();
                        break;
                }
                catch when (attempt < MAX_ATTEMPTS)
                {
                        await Task.Delay(TimeSpan.FromSeconds(2));
                }
        }
}

// Configure reverse-proxy headers, optional PathBase, exception handling, and CORS in strict order.
//
// NON-STANDARD DECISION: UseHttpsRedirection() is intentionally omitted.
// Standard ASP.NET Core templates redirect all HTTP traffic to HTTPS.
// Pros of omitting it:
// - External clients reach the Hub over HTTPS terminated at the reverse proxy (X-Forwarded-Proto: https).
// - Internal Oekolariet exhibition screens and games on the local LAN can talk over plain HTTP
//   (e.g., ASPNETCORE_URLS=http://0.0.0.0:8080) even when offline without self-signed cert failures.
// Cons:
// - Direct HTTP traffic on the internal LAN is unencrypted; rely on LAN segmentation and the reverse proxy for TLS.
app.UseHubReverseProxyAndCors(builder.Configuration);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
        app.MapOpenApi();
        app.UseSwaggerUI(options =>
        {
                // Use a relative path so Swagger UI resolves /openapi/v1.json correctly both at root (/)
                // and when mounted behind a reverse-proxy subpath (e.g., /highscore/swagger/index.html).
                options.SwaggerEndpoint("../openapi/v1.json", "HighscoreService Hub v1");
        });
}

app.MapGet("/api/v1/health", () => Results.Ok(new { Status = "Healthy", Role = "Hub" }))
        .AllowAnonymous()
        .WithName("HubHealth");

app.MapRecordHighscore();
app.MapQueryHighscores();
app.MapDeleteHighscores();
app.MapHighscoreStatistics();
app.MapHubSyncEndpoints();
app.MapScreenEnrollmentEndpoints();

await app.RunAsync();

namespace HighscoreService.Hub.Api
{
        /// <summary>
        /// Assembly marker for the Hub API host.
        /// </summary>
        public sealed class HubApiMarker
        {
        }
}

