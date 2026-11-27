using HighscoreService.Application.Interfaces;
using HighscoreService.Screen.Infrastructure.Persistence;
using HighscoreService.Screen.Infrastructure.Repositories;
using HighscoreService.Screen.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HighscoreService.Screen.Infrastructure
{
        /// <summary>
        /// Registers SQLite non-WAL persistence, UDP LAN peer discovery, and Screen sync workers.
        /// </summary>
        public static class DependencyInjection
        {
                private const int SYNC_HTTP_TIMEOUT_SECONDS = 5;

                /// <summary>
                /// Adds Screen SQLite infrastructure services.
                /// </summary>
                /// <param name="services">The service collection.</param>
                /// <param name="configuration">Application configuration.</param>
                /// <returns>The updated service collection.</returns>
                public static IServiceCollection AddHighscoreScreenInfrastructure(
                        this IServiceCollection services,
                        IConfiguration configuration)
                {
                        string connectionString = configuration.GetConnectionString("HighscoreDatabase")
                                ?? "Data Source=highscores_screen.db";

                        // Serialize all SQLite writes through a single in-process gate (non-WAL DELETE mode for power-loss safety).
                        services.AddSingleton<HighscoreWriteGate>();
                        services.AddSingleton<SqlitePragmaConnectionInterceptor>();

                        services.AddDbContext<ScreenHighscoreDbContext>((sp, db) =>
                                db.UseSqlite(connectionString)
                                  .AddInterceptors(sp.GetRequiredService<SqlitePragmaConnectionInterceptor>()));

                        services.AddHttpClient("ScreenSync", client =>
                        {
                                // Keep Hub sync timeout short so an unresponsive Hub triggers LAN peer fallback promptly.
                                client.Timeout = TimeSpan.FromSeconds(SYNC_HTTP_TIMEOUT_SECONDS);
                        });
                        services.AddScoped<IHighscoreStore, ScreenHighscoreStore>();
                        services.AddScoped<IScreenSyncStore, ScreenSyncStore>();

                        // Register zero-touch Hub identity persistence, UDP LAN peer discovery, and the Hub-first sync worker.
                        services.AddSingleton<ScreenIdentityStore>();
                        services.AddSingleton<UdpPeerDiscoveryService>();
                        services.AddHostedService(sp => sp.GetRequiredService<UdpPeerDiscoveryService>());
                        services.AddHostedService<HighscoreScreenSyncWorker>();

                        return services;
                }
        }
}

