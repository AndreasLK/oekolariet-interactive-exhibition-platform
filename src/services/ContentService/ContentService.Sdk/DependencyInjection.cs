using ContentService.Sdk.Clients;
using ContentService.Sdk.Common;
using ContentService.Sdk.Options;
using ContentService.Sdk.Protocol;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refit;

namespace ContentService.Sdk;

/// <summary>
/// Registers <c>ContentService.Sdk</c> options, startup validation, internal Refit HTTP clients,
/// and public client wrappers in dependency injection.
/// </summary>
public static class DependencyInjection
{
        /// <summary>
        /// Registers all <c>ContentService.Sdk</c> clients (<see cref="IContentServiceImageClient"/>,
        /// <see cref="IContentServiceTemplateClient"/>, and <see cref="IContentServicePlatformClient"/>)
        /// using configuration bound from the <c>ContentService</c> section.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <param name="configuration">The application configuration containing the <c>ContentService</c> section.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/> for the image Refit client to support further HTTP pipeline customization.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
        public static IHttpClientBuilder AddContentServiceSdk(
                this IServiceCollection services,
                IConfiguration configuration)
        {
                ArgumentNullException.ThrowIfNull(services);
                ArgumentNullException.ThrowIfNull(configuration);

                services.AddSingleton<IValidateOptions<ContentServiceClientOptions>, ContentServiceClientOptionsValidator>();
                services
                        .AddOptions<ContentServiceClientOptions>()
                        .Bind(configuration.GetSection(SdkConstants.CONTENT_SERVICE_SECTION_NAME))
                        .ValidateOnStart();

                return RegisterRefitClients(services: services);
        }

        /// <summary>
        /// Registers all <c>ContentService.Sdk</c> clients (<see cref="IContentServiceImageClient"/>,
        /// <see cref="IContentServiceTemplateClient"/>, and <see cref="IContentServicePlatformClient"/>)
        /// using an explicit <see cref="ContentServiceClientOptions"/> delegate.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <param name="configureOptions">A delegate configuring <see cref="ContentServiceClientOptions"/>.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/> for the image Refit client to support further HTTP pipeline customization.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configureOptions"/> is <see langword="null"/>.</exception>
        public static IHttpClientBuilder AddContentServiceSdk(
                this IServiceCollection services,
                Action<ContentServiceClientOptions> configureOptions)
        {
                ArgumentNullException.ThrowIfNull(services);
                ArgumentNullException.ThrowIfNull(configureOptions);

                services.AddSingleton<IValidateOptions<ContentServiceClientOptions>, ContentServiceClientOptionsValidator>();
                services
                        .AddOptions<ContentServiceClientOptions>()
                        .Configure(configureOptions)
                        .ValidateOnStart();

                return RegisterRefitClients(services: services);
        }

        /// <summary>
        /// Registers all <c>ContentService.Sdk</c> clients using configuration bound from the <c>ContentService</c> section.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <param name="configuration">The application configuration containing the <c>ContentService</c> section.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/> for the image Refit client.</returns>
        public static IHttpClientBuilder AddContentServiceClient(
                this IServiceCollection services,
                IConfiguration configuration) =>
                services.AddContentServiceSdk(configuration: configuration);

        /// <summary>
        /// Registers all <c>ContentService.Sdk</c> clients using an explicit <see cref="ContentServiceClientOptions"/> delegate.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <param name="configureOptions">A delegate configuring <see cref="ContentServiceClientOptions"/>.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/> for the image Refit client.</returns>
        public static IHttpClientBuilder AddContentServiceClient(
                this IServiceCollection services,
                Action<ContentServiceClientOptions> configureOptions) =>
                services.AddContentServiceSdk(configureOptions: configureOptions);

        /// <summary>
        /// Registers internal Refit endpoint clients sharing base address and timeout settings,
        /// and exposes the public thin wrapper interfaces.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/> for the image Refit client.</returns>
        private static IHttpClientBuilder RegisterRefitClients(IServiceCollection services)
        {
                RefitSettings refitSettings = RefitSettingsFactory.Create();

                // Register Refit interfaces via IHttpClientFactory so typed HttpClient lifetimes/pooling are managed properly,
                // while keeping the Refit contracts internal behind lightweight Transient public wrappers.
                IHttpClientBuilder imageClientBuilder = services
                        .AddRefitClient<IContentServiceImageEndpoints>(settings: refitSettings)
                        .ConfigureHttpClient(configureClient: ConfigureHttpClient);

                services
                        .AddRefitClient<IContentServiceTemplateEndpoints>(settings: refitSettings)
                        .ConfigureHttpClient(configureClient: ConfigureHttpClient);

                services
                        .AddRefitClient<IContentServicePlatformEndpoints>(settings: refitSettings)
                        .ConfigureHttpClient(configureClient: ConfigureHttpClient);

                services.AddTransient<IContentServiceImageClient>(static sp =>
                        new ContentServiceImageClient(
                                endpoints: sp.GetRequiredService<IContentServiceImageEndpoints>(),
                                options: sp.GetRequiredService<IOptions<ContentServiceClientOptions>>()));

                services.AddTransient<IContentServiceTemplateClient>(static sp =>
                        new ContentServiceTemplateClient(
                                endpoints: sp.GetRequiredService<IContentServiceTemplateEndpoints>(),
                                options: sp.GetRequiredService<IOptions<ContentServiceClientOptions>>()));

                services.AddTransient<IContentServicePlatformClient>(static sp =>
                        new ContentServicePlatformClient(
                                endpoints: sp.GetRequiredService<IContentServicePlatformEndpoints>(),
                                options: sp.GetRequiredService<IOptions<ContentServiceClientOptions>>()));

                return imageClientBuilder;
        }

        /// <summary>
        /// Applies normalized <see cref="ContentServiceClientOptions.BaseAddress"/> (without trailing slash for Refit route binding)
        /// and <see cref="ContentServiceClientOptions.RequestTimeout"/> to a typed Refit <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="serviceProvider">The DI service provider resolving <see cref="ContentServiceClientOptions"/>.</param>
        /// <param name="httpClient">The typed <see cref="HttpClient"/> being configured.</param>
        private static void ConfigureHttpClient(
                IServiceProvider serviceProvider,
                HttpClient httpClient)
        {
                ContentServiceClientOptions clientOptions = serviceProvider
                        .GetRequiredService<IOptions<ContentServiceClientOptions>>()
                        .Value;

                if (clientOptions.BaseAddress is not null)
                {
                        ContentServiceEndpointUriResolver.EnsureNormalizedBaseAddress(
                                httpClient: httpClient,
                                options: clientOptions);
                }

                if (clientOptions.RequestTimeout is not null)
                {
                        httpClient.Timeout = clientOptions.RequestTimeout.Value;
                }
        }
}
