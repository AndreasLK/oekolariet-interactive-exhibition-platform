using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HighscoreService.Screen.Infrastructure.Persistence
{
        /// <summary>
        /// Converts non-nullable <see cref="DateTimeOffset"/> values to UTC ticks (<see cref="long"/>) for SQLite ordering.
        /// </summary>
        internal sealed class UtcTicksConverter : ValueConverter<DateTimeOffset, long>
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="UtcTicksConverter"/> class.
                /// </summary>
                public UtcTicksConverter()
                        : base(
                                v => v.ToUniversalTime().UtcTicks,
                                v => new DateTimeOffset(v, TimeSpan.Zero))
                {
                }
        }
}

