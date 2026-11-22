using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Options;
using ContentService.Application.UseCases.Health;
using ContentService.Application.UseCases.Images;
using ContentService.Application.UseCases.Publishing;
using ContentService.Application.UseCases.Templates;
using ContentService.Application.Validation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContentService.Application;

/// <summary>
/// Registers ContentService application options, validators, and use cases with the dependency injection container.
/// </summary>
public static class DependencyInjection
{
        /// <summary>
        /// Configures <see cref="ContentStorageOptions"/> with startup validation and registers all ContentService application services and use cases.
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

                // Validate options at host startup so misconfigured quotas or storage node labels fail fast instead of waiting for the first upload.
                services.AddSingleton<IValidateOptions<ContentStorageOptions>, ContentStorageOptionsValidator>();
                services
                        .AddOptions<ContentStorageOptions>()
                        .Bind(configuration.GetSection(ApplicationConstants.CONTENT_STORAGE_SECTION_NAME))
                        .ValidateOnStart();

                // Header signature validation is stateless and thread-safe, so one shared instance works across all requests.
                services.AddSingleton<IImageSignatureValidator, ImageHeaderSignatureValidator>();

                // Register use cases as scoped so each HTTP request shares one scoped repository/DbContext instance.
                services.AddScoped<UploadImageUseCase>();
                services.AddScoped<GetImageMetadataUseCase>();
                services.AddScoped<ListImagesUseCase>();
                services.AddScoped<ListAlbumsUseCase>();
                services.AddScoped<UpdateImageMetadataUseCase>();
                services.AddScoped<GetImageStreamUseCase>();
                services.AddScoped<DeleteImageUseCase>();

                services.AddScoped<CreateTemplateUseCase>();
                services.AddScoped<UpdateTemplateUseCase>();
                services.AddScoped<GetTemplateUseCase>();
                services.AddScoped<ListTemplatesUseCase>();
                services.AddScoped<DeleteTemplateUseCase>();

                services.AddScoped<ResolvePublishBundleUseCase>();
                services.AddScoped<GetStorageHealthUseCase>();

                return services;
        }
}
