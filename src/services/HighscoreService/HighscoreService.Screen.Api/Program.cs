using HighscoreService.Application.DependencyInjection;
using HighscoreService.Screen.Api.Endpoints.Highscores;
using HighscoreService.Screen.Api.Endpoints.Sync;
using HighscoreService.Screen.Api.ExceptionHandling;
using HighscoreService.Screen.Infrastructure;
using HighscoreService.Screen.Infrastructure.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHighscoreScreenApplication();
builder.Services.AddHighscoreScreenInfrastructure(builder.Configuration);

WebApplication app = builder.Build();

using (IServiceScope scope = app.Services.CreateScope())
{
        // NON-STANDARD DECISION: Enable SQLite auto_vacuum=INCREMENTAL and EnsureCreatedAsync() on startup.
        // Pros: Zero-touch local provisioning for each exhibition screen and bounded disk growth after 7-day pruning
        // without blocking kiosk writes with a full database VACUUM.
        // Cons: Schema changes on existing screen SQLite files require explicit DDL or recreating the local cache DB.
        ScreenHighscoreDbContext dbContext = scope.ServiceProvider.GetRequiredService<ScreenHighscoreDbContext>();
        await dbContext.EnsureIncrementalAutoVacuumAsync(CancellationToken.None);
        await dbContext.Database.EnsureCreatedAsync();
}

// NON-STANDARD DECISION: UseHttpsRedirection() is intentionally omitted.
// Pros: Local game clients and LAN peer screens communicate over plain HTTP on the isolated exhibition LAN
// without self-signed certificate trust failures when offline.
// Cons: Unencrypted HTTP on the exhibition LAN; rely on physical/VLAN isolation for screen-to-screen traffic.
app.UseExceptionHandler();

app.MapGet("/api/v1/health", () => Results.Ok(new { Status = "Healthy", Role = "Screen" }))
        .WithName("ScreenHealth");

app.MapRecordHighscore();
app.MapQueryHighscores();
app.MapDeleteHighscores();
app.MapScreenDeltaEndpoint();

await app.RunAsync();

namespace HighscoreService.Screen.Api
{
        /// <summary>
        /// Assembly marker used by <c>WebApplicationFactory</c> in integration tests.
        /// </summary>
        public sealed class ScreenApiMarker
        {
        }
}

