using HighscoreService.Application.CommandHandlers;
using HighscoreService.Application.Interfaces.CommandHandlers;
using HighscoreService.Application.Interfaces.Queries;
using HighscoreService.Application.Options;
using HighscoreService.Application.QueryHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HighscoreService.Application.DependencyInjection
{
        /// <summary>
        /// Registers Highscore Service Application options, command handlers, and query handlers.
        /// </summary>
        public static class ApplicationServiceCollectionExtensions
        {
                /// <summary>
                /// Registers core command and query handlers shared by both Hub and Screen hosts.
                /// </summary>
                /// <param name="services">Service collection.</param>
                /// <returns>Updated service collection.</returns>
                public static IServiceCollection AddHighscoreCoreApplication(this IServiceCollection services)
                {
                        // Register TimeProvider.System so handlers avoid DateTimeOffset.UtcNow directly and remain testable.
                        services.TryAddSingleton(TimeProvider.System);

                        services.AddOptions<RankingOptions>()
                                .BindConfiguration(RankingOptions.SECTION_NAME)
                                .ValidateDataAnnotations()
                                .ValidateOnStart();

                        services.AddScoped<IRecordHighscoreCommandHandler, RecordHighscoreCommandHandler>();
                        services.AddScoped<IDeleteHighscoreCommandHandler, DeleteHighscoreCommandHandler>();
                        services.AddScoped<IDeleteGameHighscoresCommandHandler, DeleteGameHighscoresCommandHandler>();
                        services.AddScoped<IQueryHighscoresHandler, QueryHighscoresHandler>();

                        return services;
                }

                /// <summary>
                /// Registers Hub-only command and query handlers (statistics and central sync).
                /// </summary>
                /// <param name="services">Service collection.</param>
                /// <returns>Updated service collection.</returns>
                public static IServiceCollection AddHighscoreHubApplication(this IServiceCollection services)
                {
                        services.AddHighscoreCoreApplication();
                        services.AddScoped<IApplySyncBatchCommandHandler, ApplySyncBatchCommandHandler>();
                        services.AddScoped<IHighscoreStatisticsQueryHandler, HighscoreStatisticsQueryHandler>();
                        services.AddScoped<IHubSyncQueries, HubSyncQueries>();

                        return services;
                }

                /// <summary>
                /// Registers Screen-only options and peer delta query handlers.
                /// </summary>
                /// <param name="services">Service collection.</param>
                /// <returns>Updated service collection.</returns>
                public static IServiceCollection AddHighscoreScreenApplication(this IServiceCollection services)
                {
                        services.AddHighscoreCoreApplication();
                        services.AddOptions<ScreenSyncOptions>()
                                .BindConfiguration(ScreenSyncOptions.SECTION_NAME)
                                .ValidateDataAnnotations()
                                .ValidateOnStart();

                        services.AddScoped<IScreenDeltaQueries, ScreenDeltaQueries>();

                        return services;
                }
        }
}

