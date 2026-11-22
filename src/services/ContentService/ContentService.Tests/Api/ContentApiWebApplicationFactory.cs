using ContentService.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ContentService.Tests.Api;

/// <summary>
/// Test <see cref="WebApplicationFactory{TEntryPoint}"/> booting <c>ContentService.Api</c> against the shared SQL Server container.
/// </summary>
internal sealed class ContentApiWebApplicationFactory : WebApplicationFactory<Program>
{
        /// <summary>
        /// SQL Server connection string targeting the Testcontainers test database.
        /// </summary>
        private readonly string _connectionString;

        /// <summary>
        /// Optional per-factory configuration overrides (for quota, max upload size, and CORS tests).
        /// </summary>
        private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;

        /// <summary>
        /// Hosting environment name passed to the test web host.
        /// </summary>
        private readonly string _environmentName;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentApiWebApplicationFactory"/> class.
        /// </summary>
        /// <param name="connectionString">The active SQL Server connection string.</param>
        /// <param name="configurationOverrides">Optional configuration overrides.</param>
        /// <param name="environmentName">Target hosting environment name.</param>
        public ContentApiWebApplicationFactory(
                string connectionString,
                IReadOnlyDictionary<string, string?>? configurationOverrides = null,
                string? environmentName = null)
        {
                ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
                this._connectionString = connectionString;
                this._configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
                this._environmentName = environmentName ?? Environments.Production;
        }

        /// <inheritdoc />
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
                ArgumentNullException.ThrowIfNull(builder);

                builder.UseEnvironment(this._environmentName);
                // Apply settings via UseSetting so overrides exist before Program.cs reads builder.Configuration during registration.
                builder.UseSetting(
                        key: $"ConnectionStrings:{DependencyInjection.CONTENT_DATABASE_CONNECTION_STRING_NAME}",
                        value: this._connectionString);

                foreach (KeyValuePair<string, string?> entry in this._configurationOverrides)
                {
                        builder.UseSetting(key: entry.Key, value: entry.Value);
                }

                builder.ConfigureAppConfiguration((_, configBuilder) =>
                {
                        Dictionary<string, string?> testSettings = new(StringComparer.OrdinalIgnoreCase)
                        {
                                [$"ConnectionStrings:{DependencyInjection.CONTENT_DATABASE_CONNECTION_STRING_NAME}"] = this._connectionString
                        };

                        foreach (KeyValuePair<string, string?> entry in this._configurationOverrides)
                        {
                                testSettings[entry.Key] = entry.Value;
                        }

                        configBuilder.AddInMemoryCollection(testSettings);
                });
        }
}

