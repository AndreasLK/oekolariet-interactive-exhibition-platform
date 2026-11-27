using ContentService.Application.Common;
using ContentService.Domain.Common;
using ContentService.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace ContentService.Application.Options;

/// <summary>
/// Validates <see cref="ContentStorageOptions"/> values at host startup so misconfigured quotas or shard IDs fail fast.
/// </summary>
/// <remarks>
/// Validates via <see cref="IValidateOptions{TOptions}"/> rather than DataAnnotations so shard IDs can be verified directly through Domain <see cref="PictureReference"/> rules.
/// </remarks>
public sealed class ContentStorageOptionsValidator : IValidateOptions<ContentStorageOptions>
{
        /// <summary>
        /// Valid placeholder GUID used to probe <see cref="PictureReference.TryParse"/> for storage node ID validation.
        /// </summary>
        private const string VALIDATION_SENTINEL_GUID = "11111111-1111-4111-8111-111111111111";

        /// <summary>
        /// Validates the specified <see cref="ContentStorageOptions"/> instance.
        /// </summary>
        /// <param name="name">The named options instance being validated, or <see langword="null"/> for the default instance.</param>
        /// <param name="options">The options instance to validate.</param>
        /// <returns>A <see cref="ValidateOptionsResult"/> indicating success or detailing configuration errors.</returns>
        public ValidateOptionsResult Validate(string? name, ContentStorageOptions options)
        {
                if (options is null)
                {
                        return ValidateOptionsResult.Fail("ContentStorageOptions cannot be null.");
                }

                List<string> failures = [];
                ValidateStorageNodeId(storageNodeId: options.StorageNodeId, failures: failures);
                ValidatePeerStorageNodeIds(peerStorageNodeIds: options.PeerStorageNodeIds, failures: failures);
                ValidateSizeAndQuota(options: options, failures: failures);
                ValidateThresholds(options: options, failures: failures);

                return failures.Count > 0
                        ? ValidateOptionsResult.Fail(failures)
                        : ValidateOptionsResult.Success;
        }

        /// <summary>
        /// Validates the configured <see cref="ContentStorageOptions.StorageNodeId"/> using domain picture reference parsing rules.
        /// </summary>
        /// <param name="storageNodeId">The configured storage node identifier.</param>
        /// <param name="failures">The failure message accumulator.</param>
        private static void ValidateStorageNodeId(string? storageNodeId, List<string> failures)
        {
                if (string.IsNullOrWhiteSpace(storageNodeId))
                {
                        failures.Add("StorageNodeId cannot be null or whitespace.");
                        return;
                }

                // Probing PictureReference.TryParse guarantees Application and Domain share the exact shard regex and max-length rules without duplicating them.
                string candidateReference =
                        $"{DomainConstants.PICTURE_REFERENCE_PREFIX}{storageNodeId.Trim()}{DomainConstants.PICTURE_REFERENCE_SEGMENT_SEPARATOR}{VALIDATION_SENTINEL_GUID}";
                if (!PictureReference.TryParse(rawValue: candidateReference, reference: out _))
                {
                        failures.Add(
                                $"StorageNodeId '{storageNodeId}' must be at most {DomainConstants.MAX_STORAGE_NODE_ID_LENGTH} characters and use lowercase alphanumeric segments separated by single hyphens.");
                }
        }

        /// <summary>
        /// Validates all configured <see cref="ContentStorageOptions.PeerStorageNodeIds"/> entries against shard naming rules.
        /// </summary>
        /// <param name="peerStorageNodeIds">The configured peer storage node identifiers.</param>
        /// <param name="failures">The failure message accumulator.</param>
        private static void ValidatePeerStorageNodeIds(IReadOnlyList<string>? peerStorageNodeIds, List<string> failures)
        {
                if (peerStorageNodeIds is null)
                {
                        return;
                }

                foreach (string peerNodeId in peerStorageNodeIds)
                {
                        ValidateStorageNodeId(storageNodeId: peerNodeId, failures: failures);
                }
        }

        /// <summary>
        /// Validates image upload size limits and total node storage quota bounds.
        /// </summary>
        /// <param name="options">The options instance under validation.</param>
        /// <param name="failures">The failure message accumulator.</param>
        private static void ValidateSizeAndQuota(ContentStorageOptions options, List<string> failures)
        {
                // Keep the configured per-image ceiling within domain bounds so Application never accepts payloads Domain rejects.
                if (options.MaxImageSizeBytes < DomainConstants.MIN_IMAGE_SIZE_BYTES
                        || options.MaxImageSizeBytes > DomainConstants.MAX_IMAGE_SIZE_BYTES)
                {
                        failures.Add(
                                $"MaxImageSizeBytes must be between {DomainConstants.MIN_IMAGE_SIZE_BYTES} and {DomainConstants.MAX_IMAGE_SIZE_BYTES}.");
                }

                // Allow 0 bytes to place a storage node in read-only mode, while rejecting negative quotas.
                if (options.TotalQuotaBytes < 0)
                {
                        failures.Add("TotalQuotaBytes cannot be negative.");
                }
        }

        /// <summary>
        /// Validates that health percentage thresholds are finite, ordered, and within <c>[0, 100]</c>.
        /// </summary>
        /// <param name="options">The options instance under validation.</param>
        /// <param name="failures">The failure message accumulator.</param>
        private static void ValidateThresholds(ContentStorageOptions options, List<string> failures)
        {
                // Reject NaN and Infinity explicitly because floating-point comparisons against NaN silently evaluate to false.
                if (!double.IsFinite(options.WarningThresholdPercent)
                        || !double.IsFinite(options.CriticalThresholdPercent))
                {
                        failures.Add("WarningThresholdPercent and CriticalThresholdPercent must be finite numbers.");
                        return;
                }

                // Reject equal thresholds because WarningThresholdPercent == CriticalThresholdPercent leaves no distinct Warning band.
                if (options.WarningThresholdPercent < ApplicationConstants.MIN_THRESHOLD_PERCENT
                        || options.CriticalThresholdPercent > ApplicationConstants.MAX_THRESHOLD_PERCENT
                        || options.WarningThresholdPercent >= options.CriticalThresholdPercent)
                {
                        failures.Add(
                                $"Thresholds must satisfy {ApplicationConstants.MIN_THRESHOLD_PERCENT} <= WarningThresholdPercent < CriticalThresholdPercent <= {ApplicationConstants.MAX_THRESHOLD_PERCENT}.");
                }
        }
}

