using EditorService.Application.CommandHandlers.EditLocks;
using EditorService.Application.CommandHandlers.Games;
using EditorService.Application.CommandHandlers.SortingGames;
using EditorService.Application.CommandHandlers.SortingGames.CategoryRecievers;
using EditorService.Application.Interfaces.CommandHandlers.EditLocks;
using EditorService.Application.Interfaces.CommandHandlers.Games;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryItems;
using EditorService.Application.Interfaces.CommandHandlers.SortingGames.CategoryRecievers;
using EditorService.Application.CommandHandlers.SortingGames.CategoryItems;

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
                        // Games
                        services.AddScoped<ICreateSortingGameCommandHandler, CreateSortingGameCommandHandler>();
                        services.AddScoped<IRenameGameCommandHandler, RenameGameCommandHandler>();
                        services.AddScoped<IUpdateGameTemplateCommandHandler, UpdateGameTemplateCommandHandler>();
                        services.AddScoped<IUpdateGameThemeCommandHandler, UpdateGameThemeCommandHandler>();
                        services.AddScoped<IPublishGameCommandHandler, PublishGameCommandHandler>();
                        services.AddScoped<IDeleteGameCommandHandler, DeleteGameCommandHandler>();

                        // Categories
                        services.AddScoped<IAddCategoryReceiverCommandHandler, AddCategoryReceiverCommandHandler>();
                        services.AddScoped<IUpdateCategoryReceiverCommandHandler, UpdateCategoryReceiverCommandHandler>();
                        services.AddScoped<IRemoveCategoryReceiverCommandHandler, RemoveCategoryReceiverCommandHandler>();
                        services.AddScoped<IAddCategoryItemCommandHandler, AddCategoryItemCommandHandler>();
                        services.AddScoped<IUpdateCategoryItemCommandHandler, UpdateCategoryItemCommandHandler>();
                        services.AddScoped<IRemoveCategoryItemCommandHandler, RemoveCategoryItemCommandHandler>();

                        // Locks
                        services.AddScoped<IAcquireLockCommandHandler, AcquireLockCommandHandler>();
                        services.AddScoped<IRenewLockCommandHandler, RenewLockCommandHandler>();
                        services.AddScoped<IReleaseLockCommandHandler, ReleaseLockCommandHandler>();



                        return services;
                }
        }
}
