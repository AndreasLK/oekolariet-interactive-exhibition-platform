using ContentService.Application.Interfaces;
using ContentService.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace ContentService.Tests.Api.Fakes;

/// <summary>
/// Test <see cref="WebApplicationFactory{TEntryPoint}"/> replacing SQL Server ports with in-memory test doubles.
/// </summary>
internal sealed class FakeApiFactory : WebApplicationFactory<Program>
{
        /// <summary>
        /// Dummy SQL Server connection string satisfying startup validation when SQL Server ports are replaced with fakes.
        /// </summary>
        private const string DUMMY_CONNECTION_STRING = "Server=(localdb)\\mssqllocaldb;Database=FakeApiTest;Trusted_Connection=True;";

        private readonly InMemoryContentState _state;
        private readonly string _environmentName;
        private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;
        private readonly string? _authenticatedUserName;

        public FakeApiFactory(
                InMemoryContentState state,
                string? environmentName = null,
                IReadOnlyDictionary<string, string?>? configurationOverrides = null,
                string? authenticatedUserName = null)
        {
                this._state = state;
                this._environmentName = environmentName ?? Environments.Production;
                this._configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
                this._authenticatedUserName = authenticatedUserName;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
                builder.UseEnvironment(environment: this._environmentName);
                builder.UseSetting(
                        key: $"ConnectionStrings:{DependencyInjection.CONTENT_DATABASE_CONNECTION_STRING_NAME}",
                        value: DUMMY_CONNECTION_STRING);

                foreach (KeyValuePair<string, string?> entry in this._configurationOverrides)
                {
                        builder.UseSetting(key: entry.Key, value: entry.Value);
                }

                builder.ConfigureAppConfiguration(configureDelegate: (_, configBuilder) =>
                {
                        Dictionary<string, string?> settings = new(comparer: StringComparer.OrdinalIgnoreCase)
                        {
                                [$"ConnectionStrings:{DependencyInjection.CONTENT_DATABASE_CONNECTION_STRING_NAME}"] = DUMMY_CONNECTION_STRING
                        };

                        foreach (KeyValuePair<string, string?> entry in this._configurationOverrides)
                        {
                                settings[entry.Key] = entry.Value;
                        }

                        configBuilder.AddInMemoryCollection(initialData: settings);
                });

                builder.ConfigureTestServices(services =>
                {
                        services.RemoveAll<IImageRepository>();
                        services.RemoveAll<IImageBinaryStore>();
                        services.RemoveAll<ITemplateRepository>();
                        services.RemoveAll<IStorageQuotaMonitor>();

                        services.AddSingleton(implementationInstance: this._state);
                        services.AddScoped<IImageRepository, InMemoryImageRepository>();
                        services.AddScoped<IImageBinaryStore, InMemoryImageBinaryStore>();
                        services.AddScoped<ITemplateRepository, InMemoryTemplateRepository>();
                        services.AddScoped<IStorageQuotaMonitor, InMemoryStorageQuotaMonitor>();

                        if (!string.IsNullOrWhiteSpace(value: this._authenticatedUserName))
                        {
                                services.AddSingleton(implementationInstance: new TestUserIdentityOptions(UserName: this._authenticatedUserName));
                                services.AddAuthentication(defaultScheme: TestAuthHandler.SCHEME_NAME)
                                        .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                                                authenticationScheme: TestAuthHandler.SCHEME_NAME,
                                                configureOptions: null);
                                services.AddSingleton<IStartupFilter, AutoAuthenticateStartupFilter>();
                        }
                });
        }
}

