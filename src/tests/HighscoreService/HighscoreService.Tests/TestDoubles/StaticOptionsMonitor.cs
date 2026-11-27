using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace HighscoreService.Tests.TestDoubles
{
        /// <summary>
        /// Minimal <see cref="IOptionsMonitor{TOptions}"/> returning a static <see cref="AuthenticationSchemeOptions"/> instance.
        /// </summary>
        internal sealed class StaticOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
        {
                private readonly AuthenticationSchemeOptions _current = new();

                /// <inheritdoc />
                public AuthenticationSchemeOptions CurrentValue => this._current;

                /// <inheritdoc />
                public AuthenticationSchemeOptions Get(string? name)
                {
                        // Always return the single static options instance regardless of scheme name.
                        return this._current;
                }

                /// <inheritdoc />
                public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener)
                {
                        // No-op because static test options never change.
                        return null;
                }
        }
}

