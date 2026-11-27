using HighscoreService.Domain.Entities;
using HighscoreService.Domain.Specifications;
using HighscoreService.Domain.ValueObjects;

namespace HighscoreService.Application.Common
{
        /// <summary>
        /// Applies <see cref="HighscoreSpecification"/> filters to an <see cref="IQueryable{HighscoreEntity}"/>.
        /// </summary>
        public static class HighscoreQueryFilter
        {
                /// <summary>
                /// Applies all active specification filters to the query.
                /// </summary>
                /// <param name="query">The base query.</param>
                /// <param name="specification">The specification to apply.</param>
                /// <param name="utcNow">The current UTC timestamp for relative windows.</param>
                /// <returns>The filtered query.</returns>
                public static IQueryable<HighscoreEntity> Apply(
                        IQueryable<HighscoreEntity> query,
                        HighscoreSpecification specification,
                        DateTimeOffset utcNow)
                {
                        // Exclude soft-deleted, unnamed, and timed-out entries unless explicitly requested.
                        if (!specification.IncludeDeleted)
                        {
                                query = query.Where(e => !e.IsDeleted);
                        }

                        if (!specification.IncludeUnnamed)
                        {
                                query = query.Where(e => e.Name != null);
                        }

                        if (!specification.IncludeTimedOut)
                        {
                                query = query.Where(e => !e.IsTimedOut);
                        }

                        if (specification.GameId.HasValue)
                        {
                                query = query.Where(e => e.GameId == specification.GameId.Value);
                        }

                        // Normalize the MAC address filter before comparing against persisted canonical values.
                        if (!string.IsNullOrWhiteSpace(specification.SenderMacAddress))
                        {
                                string mac = MacAddress.Parse(specification.SenderMacAddress).Value;
                                query = query.Where(e => e.SenderMacAddress == mac);
                        }

                        // Resolve either explicit FromUtc or relative LastHours into a single lower bound.
                        DateTimeOffset? effectiveFrom = specification.ResolveEffectiveFromUtc(utcNow);
                        if (effectiveFrom.HasValue)
                        {
                                query = query.Where(e => e.PlayedAtUtc >= effectiveFrom.Value);
                        }

                        if (specification.ToUtc.HasValue)
                        {
                                DateTimeOffset toUtc = specification.ToUtc.Value.ToUniversalTime();
                                query = query.Where(e => e.PlayedAtUtc <= toUtc);
                        }

                        // Require non-null Age when either age bound is active.
                        if (specification.MinAge.HasValue)
                        {
                                query = query.Where(e => e.Age.HasValue && e.Age.Value >= specification.MinAge.Value);
                        }

                        if (specification.MaxAge.HasValue)
                        {
                                query = query.Where(e => e.Age.HasValue && e.Age.Value <= specification.MaxAge.Value);
                        }

                        return query;
                }
        }
}

