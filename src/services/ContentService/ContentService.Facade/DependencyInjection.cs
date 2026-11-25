using ContentService.Application;
using ContentService.Facade.Interfaces;
using ContentService.Facade.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Facade;

/// <summary>
/// Registers ContentService CQS facade services and underlying Application use cases with dependency injection.
/// </summary>
public static class DependencyInjection
{
        /// <summary>
        /// Registers <c>ContentService.Application</c> use cases and <c>ContentService.Facade</c> CQS contracts.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <param name="configuration">The application configuration containing ContentService settings.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.</exception>
        public static IServiceCollection AddContentServiceFacade(
                this IServiceCollection services,
                IConfiguration configuration)
        {
                ArgumentNullException.ThrowIfNull(services);
                ArgumentNullException.ThrowIfNull(configuration);

                // Register Application use cases first so scoped CQS facades can resolve them per HTTP request.
                services.AddContentServiceApplication(configuration: configuration);

                services.AddScoped<IImageCommandFacade, ImageCommandFacade>();
                services.AddScoped<IImageQueryFacade, ImageQueryFacade>();
                services.AddScoped<ITemplateCommandFacade, TemplateCommandFacade>();
                services.AddScoped<ITemplateQueryFacade, TemplateQueryFacade>();
                services.AddScoped<IContentPlatformQueryFacade, ContentPlatformQueryFacade>();
                services.AddScoped<IPoolFacade, PoolFacade>();

                return services;
        }
}

