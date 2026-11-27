using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HighscoreService.Screen.Infrastructure.Persistence
{
        /// <summary>
        /// Enforces non-WAL DELETE journal mode, FULL synchronous durability, and busy timeout on every opened SQLite connection.
        /// </summary>
        /// <remarks>
        /// NON-STANDARD DECISION: Explicitly selects <c>journal_mode=DELETE</c> + <c>synchronous=FULL</c>
        /// instead of EF Core/SQLite's common <c>WAL</c> + <c>NORMAL</c> tuning.
        /// <para>
        /// Pros: Prevents uncheckpointed WAL loss if an exhibition screen loses power abruptly.
        /// Cons: Lower write throughput, mitigated by <see cref="HighscoreWriteGate"/> and 7-day pruning.
        /// </para>
        /// </remarks>
        public sealed class SqlitePragmaConnectionInterceptor : DbConnectionInterceptor
        {
                private const string PRAGMA_SQL = "PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; PRAGMA busy_timeout=5000;";

                /// <inheritdoc />
                public override void ConnectionOpened(
                        DbConnection connection,
                        ConnectionEndEventData eventData)
                {
                        // Enforce non-WAL durability and busy-wait timeout immediately on synchronous connection open.
                        using DbCommand command = connection.CreateCommand();
                        command.CommandText = PRAGMA_SQL;
                        command.ExecuteNonQuery();
                }

                /// <inheritdoc />
                public override async Task ConnectionOpenedAsync(
                        DbConnection connection,
                        ConnectionEndEventData eventData,
                        CancellationToken cancellationToken = default)
                {
                        // Enforce non-WAL durability and busy-wait timeout immediately on async connection open.
                        await using DbCommand command = connection.CreateCommand();
                        command.CommandText = PRAGMA_SQL;
                        await command.ExecuteNonQueryAsync(cancellationToken);
                }
        }
}

