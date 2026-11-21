using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Options;
using ContentService.Application.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContentService.Application;

/// <summary>
/// Registers ContentService application options and validation services with the dependency injection container.
/// </summary>
public static class DependencyInjection
{
        /// <summary>
        /// Configures <see cref="ContentStorageOptions"/> with startup validation and registers stateless ContentService application validation services.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <param name="configuration">The application configuration containing the <c>ContentStorage</c> section.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
        public static IServiceCollection AddContentServiceApplication(
                this IServiceCollection services,
                IConfiguration configuration)
        {
                ArgumentNullException.ThrowIfNull(services);
                ArgumentNullException.ThrowIfNull(configuration);

                // Validate options at host startup so misconfigured quotas or shard identifiers never fail silently until the first upload.
                services.AddSingleton<IValidateOptions<ContentStorageOptions>, ContentStorageOptionsValidator>();
                services
                        .AddOptions<ContentStorageOptions>()
                        .Bind(configuration.GetSection(ApplicationConstants.CONTENT_STORAGE_SECTION_NAME))
                        .ValidateOnStart();

                // Magic-byte validation is stateless and thread-safe, so a single shared instance suffices across requests.
                services.AddSingleton<IImageSignatureValidator, MagicByteImageSignatureValidator>();

                return services;
        }
}

