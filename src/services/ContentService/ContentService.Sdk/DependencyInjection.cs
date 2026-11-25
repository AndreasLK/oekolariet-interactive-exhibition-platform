using ContentService.Sdk.Clients;
using ContentService.Sdk.Common;
using ContentService.Sdk.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContentService.Sdk;

/// <summary>
/// Registers <c>ContentService.Sdk</c> options, startup validation, and typed HTTP clients in dependency injection.
/// </summary>
public static class DependencyInjection
{
        /// <summary>
        /// Registers <see cref="IContentServiceImageClient"/> using configuration bound from the <c>ContentService</c> section.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <param name="configuration">The application configuration containing the <c>ContentService</c> section.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/> for further HTTP pipeline customization.</returns>
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

                return RegisterTypedImageClient(services: services);
        }

        /// <summary>
        /// Registers <see cref="IContentServiceImageClient"/> using an explicit <see cref="ContentServiceClientOptions"/> delegate.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <param name="configureOptions">A delegate configuring <see cref="ContentServiceClientOptions"/>.</param>
        /// <returns>The <see cref="IHttpClientBuilder"/> for further HTTP pipeline customization.</returns>
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

                return RegisterTypedImageClient(services: services);
        }

        private static IHttpClientBuilder RegisterTypedImageClient(IServiceCollection services)
        {
                return services.AddHttpClient<IContentServiceImageClient, ContentServiceImageClient>(
                        configureClient: static (serviceProvider, httpClient) =>
                        {
                                ContentServiceClientOptions clientOptions = serviceProvider
                                        .GetRequiredService<IOptions<ContentServiceClientOptions>>()
                                        .Value;

                                if (clientOptions.BaseAddress is not null)
                                {
                                        httpClient.BaseAddress = clientOptions.BaseAddress;
                                }

                                if (clientOptions.RequestTimeout is not null)
                                {
                                        httpClient.Timeout = clientOptions.RequestTimeout.Value;
                                }
                        });
        }
}

