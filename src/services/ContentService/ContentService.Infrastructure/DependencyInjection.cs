using ContentService.Application.Interfaces;
using ContentService.Infrastructure.Images;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Infrastructure;

/// <summary>
/// Wires SQL Server persistence adapters behind Application port interfaces so the API and Application layers stay decoupled from EF Core and database details.
/// </summary>
public static class DependencyInjection
{
        /// <summary>
        /// Configuration connection-string key shared between host configuration and integration-test database overrides.
        /// </summary>
        public const string CONTENT_DATABASE_CONNECTION_STRING_NAME = "ContentDatabase";

        /// <summary>
        /// Registers <see cref="ContentDbContext"/> and scoped SQL Server repositories while validating connection configuration eagerly at startup.
        /// </summary>
        /// <param name="services">The target service collection.</param>
        /// <param name="configuration">The application configuration providing the initial database connection string.</param>
        /// <returns>The updated <see cref="IServiceCollection"/> for fluent startup composition.</returns>
        /// <exception cref="InvalidOperationException">Thrown at startup when <c>ConnectionStrings:ContentDatabase</c> is missing or blank so misconfigured hosts fail fast before serving traffic.</exception>
        public static IServiceCollection AddContentServiceInfrastructure(
                this IServiceCollection services,
                IConfiguration configuration)
        {
                ArgumentNullException.ThrowIfNull(services);
                ArgumentNullException.ThrowIfNull(configuration);

                // Validate upfront so missing database configuration fails during host boot instead of surfacing as obscure SQL errors on the first request.
                string? connectionString = configuration.GetConnectionString(CONTENT_DATABASE_CONNECTION_STRING_NAME);
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                        throw new InvalidOperationException(
                                $"Connection string '{CONTENT_DATABASE_CONNECTION_STRING_NAME}' is not configured.");
                }

                // Resolve IConfiguration lazily per DbContext scope so WebApplicationFactory test hosts can swap in ephemeral Testcontainers connection strings cleanly.
                services.AddDbContext<ContentDbContext>((serviceProvider, options) =>
                {
                        IConfiguration? runtimeConfig = serviceProvider.GetService<IConfiguration>();
                        string effectiveConnectionString = runtimeConfig?.GetConnectionString(CONTENT_DATABASE_CONNECTION_STRING_NAME)
                                ?? connectionString;
                        options.UseSqlServer(
                                connectionString: effectiveConnectionString,
                                sqlServerOptionsAction: sqlOptions => sqlOptions.MigrationsAssembly(typeof(ContentDbContext).Assembly.FullName));
                });

                // Scope repository lifetimes to each HTTP request so DbContext instances are never shared concurrently across threads.
                services.AddScoped<IImageRepository, SqlServerImageRepository>();
                services.AddScoped<IImageBinaryStore, SqlServerImageBinaryStore>();
                services.AddScoped<ITemplateRepository, SqlServerTemplateRepository>();
                services.AddScoped<IStorageQuotaMonitor, SqlServerStorageQuotaMonitor>();
                services.AddSingleton<IImagePreviewGenerator, SkiaImagePreviewGenerator>();

                return services;
        }
}
