namespace ContentService.Domain.Exceptions;

/// <summary>
/// Represents a storage capacity failure thrown when an upload would cause a storage node to exceed its byte quota.
/// </summary>
// Exposes structured quota properties alongside the message so monitoring and API error responses do not need to parse text.
public sealed class StorageQuotaExceededException : DomainException
{
        /// <summary>
        /// Initializes a new instance of the <see cref="StorageQuotaExceededException"/> class with quota usage details.
        /// </summary>
        /// <param name="storageNodeId">The identifier of the storage node whose quota was exceeded.</param>
        /// <param name="usedBytes">The projected or current byte usage on the storage node.</param>
        /// <param name="quotaBytes">The configured maximum byte capacity of the storage node.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="storageNodeId"/> is null, empty, or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="usedBytes"/> or <paramref name="quotaBytes"/> is negative.</exception>
        public StorageQuotaExceededException(string storageNodeId, long usedBytes, long quotaBytes)
                : base(FormatMessage(storageNodeId, usedBytes, quotaBytes))
        {
                // Throws Argument* instead of DomainValidationException because invalid quota numbers indicate a caller bug, not bad user input.
                ArgumentException.ThrowIfNullOrWhiteSpace(storageNodeId);
                ArgumentOutOfRangeException.ThrowIfNegative(usedBytes);
                ArgumentOutOfRangeException.ThrowIfNegative(quotaBytes);

                this.StorageNodeId = storageNodeId.Trim();
                this.UsedBytes = usedBytes;
                this.QuotaBytes = quotaBytes;
        }

        /// <summary>
        /// Gets the normalized identifier of the storage node that exceeded its quota.
        /// </summary>
        public string StorageNodeId { get; }

        /// <summary>
        /// Gets the byte usage recorded when the quota check failed.
        /// </summary>
        public long UsedBytes { get; }

        /// <summary>
        /// Gets the maximum byte quota configured for the storage node.
        /// </summary>
        public long QuotaBytes { get; }

        /// <summary>
        /// Formats the exception message after validating quota arguments for the base constructor.
        /// </summary>
        /// <param name="storageNodeId">The raw storage node identifier.</param>
        /// <param name="usedBytes">The projected or current byte usage.</param>
        /// <param name="quotaBytes">The configured byte quota.</param>
        /// <returns>A formatted error message describing the quota overrun.</returns>
        private static string FormatMessage(string storageNodeId, long usedBytes, long quotaBytes)
        {
                ArgumentException.ThrowIfNullOrWhiteSpace(storageNodeId);
                ArgumentOutOfRangeException.ThrowIfNegative(usedBytes);
                ArgumentOutOfRangeException.ThrowIfNegative(quotaBytes);

                return $"Storage node '{storageNodeId.Trim()}' has exceeded its storage quota ({usedBytes} / {quotaBytes} bytes).";
        }
}
