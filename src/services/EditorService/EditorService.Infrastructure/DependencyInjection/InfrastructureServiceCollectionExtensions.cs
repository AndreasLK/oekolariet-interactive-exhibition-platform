using EditorService.Application.Interfaces.Repositories.Games;
using EditorService.Infrastructure.Persistence;
using EditorService.Infrastructure.Repositories.Games;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace EditorService.Infrastructure.DependencyInjection
{
        /// <summary>
        /// Registers everything in the Infrastructure layer.
        /// </summary>
        public static class InfrastructureServiceCollectionExtensions
        {
                /// <summary>
                /// Adds the database and the repositories.
                /// </summary>
                /// <param name="services">The service collection from Program.cs.</param>
                /// <param name="connectionString">The connection string to the Editor database.</param>
                /// <returns>The same service collection, so calls can be chained.</returns>
                public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
                {
                        // EF Core talks to SQL Server with this connection string.
                        services.AddDbContext<EditorDbContext>(optionsAction: options => options.UseSqlServer(connectionString: connectionString));

                        // When a handler asks for IGameRepository, it gets the EF Core version.
                        services.AddScoped<IGameRepository, GameRepository>();

                        return services;
                }
        }
}
