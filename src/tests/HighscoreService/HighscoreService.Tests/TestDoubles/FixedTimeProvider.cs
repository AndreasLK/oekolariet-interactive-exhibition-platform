namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// Deterministic <see cref="TimeProvider"/> returning a fixed UTC timestamp.
        /// </summary>
        /// <param name="utcNow">Fixed UTC timestamp returned by <see cref="GetUtcNow"/>.</param>
        internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
        {
                private readonly DateTimeOffset _utcNow = utcNow;

                /// <inheritdoc />
                public override DateTimeOffset GetUtcNow()
                {
                        // Return the configured deterministic UTC timestamp.
                        return this._utcNow;
                }
        }
}

