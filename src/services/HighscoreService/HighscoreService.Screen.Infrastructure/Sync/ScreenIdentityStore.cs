using System.Security.Cryptography;
using System.Text.Json;
using HighscoreService.Application.Options;
using Microsoft.Extensions.Options;

namespace HighscoreService.Screen.Infrastructure.Sync
{
        /// <summary>
        /// Persists and loads this exhibition screen's auto-generated Hub credential (<c>ScreenId</c> + random secret).
        /// </summary>
        /// <remarks>
        /// NON-STANDARD DECISION: Generates a 32-byte random secret at runtime on first boot and stores it in a
        /// local JSON file instead of embedding a shared secret in binaries or requiring non-technical staff to edit <c>.env</c> files.
        /// <para>
        /// Pros: Ghidra/decompilation finds no secrets in binaries, and installers only boot the Screen server.
        /// Cons: Wiping the identity file generates a new <see cref="ScreenIdentity.ScreenId"/> that requires admin re-approval on the Hub.
        /// </para>
        /// </remarks>
        public sealed class ScreenIdentityStore(IOptions<ScreenSyncOptions> options)
        {
                private const int SECRET_BYTE_LENGTH = 32;

                private readonly string _filePath = ResolveFilePath(options.Value.IdentityFilePath);
                private readonly SemaphoreSlim _gate = new(1, 1);
                private ScreenIdentity? _cached;

                /// <summary>
                /// Returns the existing screen identity or generates and persists a new one on first boot.
                /// </summary>
                /// <param name="cancellationToken">Cancellation token.</param>
                /// <returns>The screen identity credential.</returns>
                public async Task<ScreenIdentity> GetOrCreateAsync(CancellationToken cancellationToken)
                {
                        // Fast-path return when the identity is already cached in memory.
                        if (this._cached is not null)
                        {
                                return this._cached;
                        }

                        // Serialize first-time file creation across concurrent callers.
                        await this._gate.WaitAsync(cancellationToken);
                        try
                        {
                                if (this._cached is not null)
                                {
                                        return this._cached;
                                }

                                // Reuse existing persisted identity if valid.
                                if (File.Exists(this._filePath))
                                {
                                        string existingJson = await File.ReadAllTextAsync(this._filePath, cancellationToken);
                                        ScreenIdentity? loaded = JsonSerializer.Deserialize<ScreenIdentity>(existingJson);
                                        if (loaded is not null && loaded.ScreenId != Guid.Empty && !string.IsNullOrWhiteSpace(loaded.Secret))
                                        {
                                                this._cached = loaded;
                                                return loaded;
                                        }
                                }

                                // Generate a fresh UUIDv7 screen identifier and cryptographically random secret.
                                ScreenIdentity created = new(
                                        ScreenId: Guid.CreateVersion7(),
                                        Secret: Convert.ToBase64String(RandomNumberGenerator.GetBytes(SECRET_BYTE_LENGTH)));

                                string? directory = Path.GetDirectoryName(this._filePath);
                                if (!string.IsNullOrWhiteSpace(directory))
                                {
                                        Directory.CreateDirectory(directory);
                                }

                                string json = JsonSerializer.Serialize(created);
                                await File.WriteAllTextAsync(this._filePath, json, cancellationToken);

                                this._cached = created;
                                return created;
                        }
                        finally
                        {
                                this._gate.Release();
                        }
                }

                private static string ResolveFilePath(string? configuredPath)
                {
                        // Fall back to a file adjacent to the application binary when no explicit path is configured.
                        if (!string.IsNullOrWhiteSpace(configuredPath))
                        {
                                return Path.GetFullPath(configuredPath.Trim());
                        }

                        return Path.Combine(AppContext.BaseDirectory, "screen-identity.json");
                }
        }
}

