namespace ContentService.Domain.Exceptions;

// Thrown when uploading binary content would push a storage node past its byte quota.
public sealed class StorageQuotaExceededException : DomainException
{
        public StorageQuotaExceededException(string storageNodeId, long usedBytes, long quotaBytes)
                : base(message: BuildMessage(storageNodeId: storageNodeId, usedBytes: usedBytes, quotaBytes: quotaBytes))
        {
                this.StorageNodeId = storageNodeId.Trim();
                this.UsedBytes = usedBytes;
                this.QuotaBytes = quotaBytes;
        }

        public string StorageNodeId { get; }

        public long UsedBytes { get; }

        public long QuotaBytes { get; }

        // Static helper is required because C# does not allow calling instance methods inside ': base(...)' before the object is constructed.
        private static string BuildMessage(string storageNodeId, long usedBytes, long quotaBytes)
        {
                if (string.IsNullOrWhiteSpace(value: storageNodeId))
                {
                        throw new ArgumentException(message: "Storage node ID cannot be null or whitespace.", paramName: nameof(storageNodeId));
                }

                if (usedBytes < 0)
                {
                        throw new ArgumentOutOfRangeException(paramName: nameof(usedBytes), message: "Used bytes cannot be negative.");
                }

                if (quotaBytes < 0)
                {
                        throw new ArgumentOutOfRangeException(paramName: nameof(quotaBytes), message: "Quota bytes cannot be negative.");
                }

                return $"Storage node '{storageNodeId.Trim()}' has exceeded its storage quota ({usedBytes} / {quotaBytes} bytes).";
        }
}
