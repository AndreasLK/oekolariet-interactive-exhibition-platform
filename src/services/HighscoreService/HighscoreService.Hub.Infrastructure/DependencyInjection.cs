using HighscoreService.Application.Interfaces;
using HighscoreService.Hub.Infrastructure.Persistence;
using HighscoreService.Hub.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HighscoreService.Hub.Infrastructure
{
        /// <summary>
        /// Registers SQL Server persistence and Hub sync/statistics stores.
        /// </summary>
        public static class DependencyInjection
        {
                /// <summary>
                /// Adds Hub SQL Server infrastructure services.
                /// </summary>
                /// <param name="services">The service collection.</param>
                /// <param name="configuration">Application configuration.</param>
                /// <returns>The updated service collection.</returns>
                public static IServiceCollection AddHighscoreHubInfrastructure(
                        this IServiceCollection services,
                        IConfiguration configuration)
                {
                        // Fail fast at startup if the central Hub SQL Server connection string is missing.
                        string connectionString = configuration.GetConnectionString("HighscoreDatabase")
                                ?? throw new InvalidOperationException("ConnectionStrings:HighscoreDatabase is required for Hub.");

                        services.AddDbContext<HubHighscoreDbContext>(db =>
                                db.UseSqlServer(connectionString));

                        // Register Hub-specific SQL Server repositories and screen enrollment store.
                        services.AddScoped<IHighscoreStore, HubHighscoreStore>();
                        services.AddScoped<IHubStatisticsStore, HubStatisticsStore>();
                        services.AddScoped<IHubSyncStore, HubSyncIngestStore>();
                        services.AddScoped<IScreenRegistrationStore, HubScreenRegistrationStore>();

                        return services;
                }
        }
}

