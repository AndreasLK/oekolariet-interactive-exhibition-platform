using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HighscoreService.Screen.Infrastructure.Persistence
{
        /// <summary>
        /// Converts nullable <see cref="DateTimeOffset"/> values to nullable UTC ticks (<see cref="long"/>) for SQLite ordering.
        /// </summary>
        internal sealed class NullableUtcTicksConverter : ValueConverter<DateTimeOffset?, long?>
        {
                /// <summary>
                /// Initializes a new instance of the <see cref="NullableUtcTicksConverter"/> class.
                /// </summary>
                public NullableUtcTicksConverter()
                        : base(
                                v => v.HasValue ? v.Value.ToUniversalTime().UtcTicks : null,
                                v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null)
                {
                }
        }
}

