namespace ContentService.Domain.Exceptions;

public sealed class StorageQuotaExceededException : DomainException
{
        public StorageQuotaExceededException(string nodeId, long usedBytes, long maxQuotaBytes)
                : base(message: BuildMessage(nodeId: nodeId, usedBytes: usedBytes, maxQuotaBytes: maxQuotaBytes))
        {
                this.NodeId = nodeId.Trim();
                this.UsedBytes = usedBytes;
                this.MaxQuotaBytes = maxQuotaBytes;
        }

        public string NodeId { get; }

        public long UsedBytes { get; }

        public long MaxQuotaBytes { get; }

        private static string BuildMessage(string nodeId, long usedBytes, long maxQuotaBytes)
        {
                if (string.IsNullOrWhiteSpace(value: nodeId))
                {
                        throw new DomainValidationException(message: "Node ID cannot be null or whitespace.");
                }

                if (usedBytes < 0)
                {
                        throw new DomainValidationException(message: "Used bytes cannot be negative.");
                }

                if (maxQuotaBytes <= 0)
                {
                        throw new DomainValidationException(message: "Maximum quota bytes must be positive.");
                }

                return $"Storage node '{nodeId.Trim()}' has exceeded its storage quota ({usedBytes} / {maxQuotaBytes} bytes).";
        }
}

