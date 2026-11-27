namespace HighscoreService.Domain.Specifications
{
        /// <summary>
        /// Serializable query specification for filtering and paging highscore entries.
        /// </summary>
        public sealed record HighscoreSpecification
        {
                /// <summary>
                /// Default page number when not specified.
                /// </summary>
                public const int DEFAULT_PAGE = 1;

                /// <summary>
                /// Default page size when not specified.
                /// </summary>
                public const int DEFAULT_PAGE_SIZE = 50;

                /// <summary>
                /// Maximum allowed page size to protect database and network bandwidth.
                /// </summary>
                public const int MAX_PAGE_SIZE = 500;

                /// <summary>
                /// Gets the optional game identifier filter.
                /// </summary>
                public Guid? GameId { get; init; }

                /// <summary>
                /// Gets the optional screen MAC address filter.
                /// </summary>
                public string? SenderMacAddress { get; init; }

                /// <summary>
                /// Gets the optional inclusive UTC start timestamp filter.
                /// </summary>
                public DateTimeOffset? FromUtc { get; init; }

                /// <summary>
                /// Gets the optional inclusive UTC end timestamp filter.
                /// </summary>
                public DateTimeOffset? ToUtc { get; init; }

                /// <summary>
                /// Gets the optional rolling lookback window in hours (e.g. 5 for the last 5 hours).
                /// </summary>
                public int? LastHours { get; init; }

                /// <summary>
                /// Gets the optional minimum player age filter (inclusive).
                /// </summary>
                public int? MinAge { get; init; }

                /// <summary>
                /// Gets the optional maximum player age filter (inclusive).
                /// </summary>
                public int? MaxAge { get; init; }

                /// <summary>
                /// Gets a value indicating whether entries without a player name are included.
                /// </summary>
                public bool IncludeUnnamed { get; init; }

                /// <summary>
                /// Gets a value indicating whether timed-out/abandoned entries are included.
                /// </summary>
                public bool IncludeTimedOut { get; init; }

                /// <summary>
                /// Gets a value indicating whether soft-deleted tombstone entries are included.
                /// </summary>
                public bool IncludeDeleted { get; init; }

                /// <summary>
                /// Gets the 1-based page number.
                /// </summary>
                public int Page { get; init; } = DEFAULT_PAGE;

                /// <summary>
                /// Gets the maximum number of items per page.
                /// </summary>
                public int PageSize { get; init; } = DEFAULT_PAGE_SIZE;

                /// <summary>
                /// Resolves the effective inclusive UTC start timestamp using <see cref="FromUtc"/> or <see cref="LastHours"/>.
                /// </summary>
                /// <param name="utcNow">The current UTC reference time.</param>
                /// <returns>The resolved UTC start bound, or null if unbounded.</returns>
                public DateTimeOffset? ResolveEffectiveFromUtc(DateTimeOffset utcNow)
                {
                        if (this.FromUtc.HasValue)
                        {
                                return this.FromUtc.Value.ToUniversalTime();
                        }

                        if (this.LastHours.HasValue && this.LastHours.Value > 0)
                        {
                                return utcNow.ToUniversalTime().AddHours(-this.LastHours.Value);
                        }

                        return null;
                }

                /// <summary>
                /// Returns a normalized copy with clamped <see cref="Page"/> and <see cref="PageSize"/> values.
                /// </summary>
                /// <returns>A normalized <see cref="HighscoreSpecification"/>.</returns>
                public HighscoreSpecification NormalizePaging()
                {
                        int normalizedPage = this.Page < DEFAULT_PAGE ? DEFAULT_PAGE : this.Page;
                        int normalizedSize = this.PageSize < 1
                                ? DEFAULT_PAGE_SIZE
                                : Math.Min(this.PageSize, MAX_PAGE_SIZE);

                        return this with
                        {
                                Page = normalizedPage,
                                PageSize = normalizedSize
                        };
                }
        }
}

