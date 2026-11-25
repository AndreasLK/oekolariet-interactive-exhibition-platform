using EditorService.Application.CommandHandlers.Games;
using EditorService.Application.Interfaces.CommandHandlers.Games;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Application.DependencyInjection
{
        /// <summary>
        /// Registers everything in the Application layer.
        /// </summary>
        public static class ApplicationServiceCollectionExtensions
        {
                /// <summary>
                /// Adds the command handlers and the services they need.
                /// </summary>
                /// <param name="services">The service collection from Program.cs.</param>
                /// <returns>The same service collection, so calls can be chained.</returns>
                public static IServiceCollection AddApplication(this IServiceCollection services)
                {
                        // Registers the system clock (UTC time from the server, never from the client).
                        // Handlers depend on TimeProvider instead of DateTimeOffset.UtcNow,
                        // so tests can replace it with a fake clock.
                        services.TryAddSingleton(instance: TimeProvider.System);

                        // One line per command handler.
                        services.AddScoped<ICreateSortingGameCommandHandler, CreateSortingGameCommandHandler>();

                        return services;
                }
        }
}
