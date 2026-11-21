using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Health;

/// <summary>
/// Evaluates storage quota consumption and maps utilization against configured warning and critical read-only thresholds.
/// </summary>
public sealed class GetStorageHealthUseCase
{
        /// <summary>
        /// Storage node byte consumption monitor.
        /// </summary>
        private readonly IStorageQuotaMonitor _quotaMonitor;

        /// <summary>
        /// Configured quota budget and health thresholds.
        /// </summary>
        private readonly ContentStorageOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetStorageHealthUseCase"/> class.
        /// </summary>
        /// <param name="quotaMonitor">The storage byte consumption monitor.</param>
        /// <param name="options">The configured content storage options.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
        public GetStorageHealthUseCase(
                IStorageQuotaMonitor quotaMonitor,
                IOptions<ContentStorageOptions> options)
        {
                ArgumentNullException.ThrowIfNull(quotaMonitor);
                ArgumentNullException.ThrowIfNull(options);
                ArgumentNullException.ThrowIfNull(options.Value);

                this._quotaMonitor = quotaMonitor;
                this._options = options.Value;
        }

        /// <summary>
        /// Computes current disk utilization percentage and classifies the storage node's operational health status.
        /// </summary>
        /// <param name="cancellationToken">A token to observe while querying current byte usage.</param>
        /// <returns>A <see cref="StorageHealthResult"/> containing status, byte counts, and utilization percentage.</returns>
        public async Task<StorageHealthResult> ExecuteAsync(CancellationToken cancellationToken)
        {
                long usedBytes = await this._quotaMonitor.GetUsedBytesAsync(cancellationToken: cancellationToken);
                long safeUsedBytes = Math.Max(val1: 0L, val2: usedBytes);
                long quotaBytes = this._options.TotalQuotaBytes;

                // Treat zero or negative quotas as completely exhausted to avoid division-by-zero and safely block new writes.
                if (quotaBytes <= 0)
                {
                        return new StorageHealthResult
                        {
                                Status = StorageHealthStatus.FullReadOnly,
                                UsedBytes = safeUsedBytes,
                                QuotaBytes = Math.Max(val1: 0L, val2: quotaBytes),
                                UtilizationPercent = ApplicationConstants.FULL_UTILIZATION_PERCENT
                        };
                }

                // Perform division in decimal so exact percentages (e.g., 29/100) do not fall below threshold via IEEE-754 rounding.
                decimal exactUtilization = ((decimal)safeUsedBytes * ApplicationConstants.PERCENT_MULTIPLIER) / quotaBytes;
                StorageHealthStatus status = this.DetermineHealthStatus(exactUtilization: exactUtilization);

                return new StorageHealthResult
                {
                        Status = status,
                        UsedBytes = safeUsedBytes,
                        QuotaBytes = quotaBytes,
                        UtilizationPercent = (double)exactUtilization
                };
        }

        /// <summary>
        /// Maps an exact utilization percentage against configured warning and critical thresholds.
        /// </summary>
        /// <param name="exactUtilization">The exact decimal utilization percentage.</param>
        /// <returns>The classified <see cref="StorageHealthStatus"/>.</returns>
        private StorageHealthStatus DetermineHealthStatus(decimal exactUtilization)
        {
                // Fail safe to FullReadOnly if unvalidated NaN/Infinity thresholds are ever supplied manually.
                if (!double.IsFinite(this._options.WarningThresholdPercent)
                        || !double.IsFinite(this._options.CriticalThresholdPercent))
                {
                        return StorageHealthStatus.FullReadOnly;
                }

                if (exactUtilization >= (decimal)this._options.CriticalThresholdPercent)
                {
                        return StorageHealthStatus.FullReadOnly;
                }

                if (exactUtilization >= (decimal)this._options.WarningThresholdPercent)
                {
                        return StorageHealthStatus.Warning;
                }

                return StorageHealthStatus.Healthy;
        }
}

