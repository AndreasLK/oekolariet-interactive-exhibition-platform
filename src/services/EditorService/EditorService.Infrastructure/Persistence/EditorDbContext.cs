using EditorService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EditorService.Infrastructure.Persistence
{
        /// <summary>
        /// The connection between the domain and the Editor database.
        /// </summary>
        public sealed class EditorDbContext : DbContext
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="EditorDbContext"/> class.
                /// </summary>
                /// <param name="options">The database options, set up in DependencyInjection.</param>
                public EditorDbContext(DbContextOptions<EditorDbContext> options)
                        : base(options: options)
                {
                }

                /// <summary>Gets all games. Sorting games and quizzes are stored in the same table.</summary>
                public DbSet<GameEntity> Games => this.Set<GameEntity>();

                /// <summary>
                /// Applies all configurations in the Configurations folder.
                /// </summary>
                /// <param name="modelBuilder">The builder used to set up the tables.</param>
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                        modelBuilder.ApplyConfigurationsFromAssembly(assembly: typeof(EditorDbContext).Assembly);
                }


        }
}
