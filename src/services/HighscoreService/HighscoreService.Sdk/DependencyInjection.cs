using HighscoreService.Sdk.Client;
using Microsoft.Extensions.DependencyInjection;

namespace HighscoreService.Sdk
{
        /// <summary>
        /// Registers the typed <see cref="IHighscoreServiceClient"/> in dependency injection.
        /// </summary>
        public static class DependencyInjection
        {
                private const string PRIMARY_CLIENT_NAME = "HighscorePrimary";
                private const string FALLBACK_CLIENT_NAME = "HighscoreFallback";

                /// <summary>
                /// Registers <see cref="IHighscoreServiceClient"/> with a single base address.
                /// </summary>
                /// <param name="services">The service collection.</param>
                /// <param name="baseAddress">The Highscore Service API base address.</param>
                /// <returns>The HTTP client builder for further configuration.</returns>
                public static IHttpClientBuilder AddHighscoreServiceClient(
                        this IServiceCollection services,
                        Uri baseAddress)
                {
                        return services.AddHttpClient<IHighscoreServiceClient, HighscoreServiceClient>(client =>
                        {
                                // Ensure trailing '/' so reverse-proxy subpaths (e.g. /highscore/) are preserved.
                                client.BaseAddress = HighscoreServiceClient.NormalizeBaseAddress(baseAddress);
                        });
                }

                /// <summary>
                /// Registers <see cref="IHighscoreServiceClient"/> with Hub-first and local Screen fallback support.
                /// </summary>
                /// <param name="services">The service collection.</param>
                /// <param name="options">Client failover options.</param>
                /// <param name="configurePrimaryClient">
                /// Optional builder callback applied ONLY to the primary (Hub) <see cref="HttpClient"/>,
                /// ensuring Hub bearer credentials are never forwarded to the local Screen fallback client.
                /// </param>
                /// <returns>The updated service collection.</returns>
                public static IServiceCollection AddHighscoreServiceClient(
                        this IServiceCollection services,
                        HighscoreServiceClientOptions options,
                        Action<IHttpClientBuilder>? configurePrimaryClient = null)
                {
                        IHttpClientBuilder primaryBuilder = services.AddHttpClient(PRIMARY_CLIENT_NAME, client =>
                        {
                                client.BaseAddress = HighscoreServiceClient.NormalizeBaseAddress(options.PrimaryBaseAddress);
                        });
                        configurePrimaryClient?.Invoke(primaryBuilder);

                        if (options.FallbackBaseAddress is not null)
                        {
                                services.AddHttpClient(FALLBACK_CLIENT_NAME, client =>
                                {
                                        client.BaseAddress = HighscoreServiceClient.NormalizeBaseAddress(options.FallbackBaseAddress);
                                });
                        }

                        services.AddTransient<IHighscoreServiceClient>(sp =>
                        {
                                IHttpClientFactory factory = sp.GetRequiredService<IHttpClientFactory>();
                                HttpClient primary = factory.CreateClient(PRIMARY_CLIENT_NAME);
                                HttpClient? fallback = options.FallbackBaseAddress is not null
                                        ? factory.CreateClient(FALLBACK_CLIENT_NAME)
                                        : null;

                                return new HighscoreServiceClient(
                                        primaryHttpClient: primary,
                                        fallbackHttpClient: fallback,
                                        hubTimeoutSeconds: options.HubTimeoutSeconds);
                        });

                        return services;
                }
        }
}
