using System.Security.Cryptography;
using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using ContentService.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Orchestrates fail-fast validation, bounded stream buffering, file-header verification, quota enforcement, and atomic image persistence so invalid or spoofed uploads cannot exhaust memory or disk.
/// </summary>
public sealed class UploadImageUseCase
{
        /// <summary>
        /// Persists metadata and binary rows atomically so neither can exist without the other.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Supplies current storage byte usage so uploads fail cleanly with 507 before exhausting configured storage.
        /// </summary>
        private readonly IStorageQuotaMonitor _quotaMonitor;

        /// <summary>
        /// Verifies the file's leading header bytes so renamed scripts or mismatched files cannot bypass MIME allowlists.
        /// </summary>
        private readonly IImageSignatureValidator _signatureValidator;

        /// <summary>
        /// Provides the logical storage node label, per-image size ceiling, and total quota budget.
        /// </summary>
        private readonly ContentStorageOptions _options;

        /// <summary>
        /// Captures persistence, quota, signature, and storage-limit dependencies so upload security and capacity checks stay testable without SQL Server.
        /// </summary>
        /// <param name="imageRepository">The image metadata and binary persistence repository.</param>
        /// <param name="quotaMonitor">The storage byte usage monitor.</param>
        /// <param name="signatureValidator">The image file-header signature validator.</param>
        /// <param name="options">The configured content storage options.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
        public UploadImageUseCase(
                IImageRepository imageRepository,
                IStorageQuotaMonitor quotaMonitor,
                IImageSignatureValidator signatureValidator,
                IOptions<ContentStorageOptions> options)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                ArgumentNullException.ThrowIfNull(quotaMonitor);
                ArgumentNullException.ThrowIfNull(signatureValidator);
                ArgumentNullException.ThrowIfNull(options);
                ArgumentNullException.ThrowIfNull(options.Value);

                this._imageRepository = imageRepository;
                this._quotaMonitor = quotaMonitor;
                this._signatureValidator = signatureValidator;
                this._options = options.Value;
        }

        /// <summary>
        /// Executes cheapest-first checks before reading stream bytes, then verifies actual byte size, file-header bytes, and quota prior to atomic persistence.
        /// </summary>
        /// <param name="command">The image upload command carrying stream content, MIME type, file name, and actor audit identity.</param>
        /// <param name="cancellationToken">Propagates caller cancellation while buffering the stream and writing to storage.</param>
        /// <returns>The persisted <see cref="ImageMetadataResult"/> including its generated picture reference and SHA-256 digest.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when stream, size, MIME allowlist, file-header signature, or entity invariants fail.</exception>
        /// <exception cref="StorageQuotaExceededException">Thrown when the upload would exceed the configured byte quota.</exception>
        public async Task<ImageMetadataResult> ExecuteAsync(
                UploadImageCommand command,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(command);

                // Reject invalid metadata, MIME types, and declared sizes before querying the database or allocating stream buffers.
                long effectiveMaxSizeBytes = this.ValidatePreconditions(command: command);

                long currentUsedBytes = await this._quotaMonitor.GetUsedBytesAsync(cancellationToken: cancellationToken);
                // Short-circuit based on declared length first so obvious over-quota uploads fail before reading megabytes from the network.
                this.EnsureWithinQuota(currentUsedBytes: currentUsedBytes, incomingBytes: command.DeclaredLengthBytes);

                // Enforce a hard read ceiling during streaming so spoofed Content-Length headers cannot trigger unbounded memory allocation.
                byte[] payload = await BoundedImageStreamReader.ReadPayloadWithLimitAsync(
                        content: command.Content,
                        maxAllowedBytes: effectiveMaxSizeBytes,
                        cancellationToken: cancellationToken);
                string sha256Hash = Convert.ToHexStringLower(SHA256.HashData(payload));

                // Inspect only the leading header bytes (PNG/JPEG/WebP format markers) so renamed or spoofed files are caught even if their Content-Type claims an image.
                int headerLength = Math.Min(val1: payload.Length, val2: this._signatureValidator.RequiredHeaderByteCount);
                if (!this._signatureValidator.IsValidSignature(
                        headerBytes: payload[..headerLength],
                        declaredContentType: command.ContentType))
                {
                        throw new DomainValidationException(
                                $"Uploaded binary payload does not match declared content type '{command.ContentType}'.");
                }

                // Re-check quota using actual read bytes because callers can misdeclare Content-Length below the real stream length.
                this.EnsureWithinQuota(currentUsedBytes: currentUsedBytes, incomingBytes: payload.Length);

                ImageEntity image = ImageEntity.Create(
                        storageNodeId: this._options.StorageNodeId,
                        fileName: command.FileName,
                        contentType: command.ContentType,
                        fileSizeBytes: payload.Length,
                        sha256Hash: sha256Hash,
                        actorId: command.ActorId,
                        altText: command.AltText,
                        albumPath: command.AlbumPath,
                        tags: command.Tags);
                ImageBinaryEntity binary = ImageBinaryEntity.Create(imageId: image.Id, data: payload);

                await this._imageRepository.AddAsync(
                        metadata: image,
                        binary: binary,
                        cancellationToken: cancellationToken);

                return ContentResultMapper.ToMetadataResult(image: image);
        }

        /// <summary>
        /// Fails fast on cheap stream/size/text checks and caps configured upload limits at the domain ceiling before any buffering occurs.
        /// </summary>
        /// <param name="command">The upload command to validate.</param>
        /// <returns>The effective maximum upload size in bytes.</returns>
        /// <exception cref="DomainValidationException">Thrown when stream, size, file name, MIME type, actor ID, or alt text fails fast validation.</exception>
        private long ValidatePreconditions(UploadImageCommand command)
        {
                if (command.Content is null || !command.Content.CanRead)
                {
                        throw new DomainValidationException("Upload content stream must be non-null and readable.");
                }

                // Clamp to DomainConstants.MAX_IMAGE_SIZE_BYTES so misconfigured host options can tighten limits but never bypass the domain's 10 MiB safety ceiling.
                long effectiveMaxSizeBytes = Math.Min(val1: this._options.MaxImageSizeBytes, val2: DomainConstants.MAX_IMAGE_SIZE_BYTES);
                if (effectiveMaxSizeBytes < DomainConstants.MIN_IMAGE_SIZE_BYTES)
                {
                        throw new DomainValidationException(
                                $"Configured maximum image size must be at least {DomainConstants.MIN_IMAGE_SIZE_BYTES} byte(s).");
                }

                if (command.DeclaredLengthBytes < DomainConstants.MIN_IMAGE_SIZE_BYTES
                        || command.DeclaredLengthBytes > effectiveMaxSizeBytes)
                {
                        throw new DomainValidationException(
                                $"Declared image size must be between {DomainConstants.MIN_IMAGE_SIZE_BYTES} and {effectiveMaxSizeBytes} bytes.");
                }

                ValidateTextMetadata(command: command);
                return effectiveMaxSizeBytes;
        }

        /// <summary>
        /// Validates metadata strings prior to stream I/O so bad filenames, actors, or unsupported MIME types reject without wasting network or memory bandwidth.
        /// </summary>
        /// <param name="command">The upload command carrying metadata strings.</param>
        /// <exception cref="DomainValidationException">Thrown when any string metadata violates domain rules.</exception>
        private static void ValidateTextMetadata(UploadImageCommand command)
        {
                if (string.IsNullOrWhiteSpace(command.FileName)
                        || command.FileName.Trim().Length > DomainConstants.MAX_FILE_NAME_LENGTH)
                {
                        throw new DomainValidationException(
                                $"File name must be non-empty and cannot exceed {DomainConstants.MAX_FILE_NAME_LENGTH} characters.");
                }

                if (string.IsNullOrWhiteSpace(command.ActorId)
                        || command.ActorId.Trim().Length > DomainConstants.MAX_ACTOR_ID_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Actor ID must be non-empty and cannot exceed {DomainConstants.MAX_ACTOR_ID_LENGTH} characters.");
                }

                if (string.IsNullOrWhiteSpace(command.ContentType))
                {
                        throw new DomainValidationException("Content type cannot be null or whitespace.");
                }

                // Normalize casing and whitespace so clients sending uppercase media types still match the lowercase domain allowlist.
                string normalizedContentType = command.ContentType.Trim().ToLowerInvariant();
                if (!DomainConstants.ALLOWED_IMAGE_CONTENT_TYPES.Contains(normalizedContentType))
                {
                        throw new DomainValidationException(
                                $"Content type '{normalizedContentType}' is not an allowed image content type.");
                }

                // Allow omitted/blank AltText while bounding non-empty descriptions to the database column limit.
                if (!string.IsNullOrWhiteSpace(command.AltText)
                        && command.AltText.Trim().Length > DomainConstants.MAX_ALT_TEXT_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Alternative text cannot exceed {DomainConstants.MAX_ALT_TEXT_LENGTH} characters.");
                }

                // Validate optional album hierarchy and tags up front so invalid strings fail before quota or stream buffering.
                _ = AlbumPath.NormalizeOptional(rawAlbumPath: command.AlbumPath);
                _ = ImageTags.NormalizeOptional(rawTags: command.Tags);
        }

        /// <summary>
        /// Prevents node disk exhaustion using overflow-safe subtraction instead of addition near <see cref="long.MaxValue"/>.
        /// </summary>
        /// <param name="currentUsedBytes">The current byte usage reported by <see cref="IStorageQuotaMonitor"/>.</param>
        /// <param name="incomingBytes">The byte size of the incoming image payload.</param>
        /// <exception cref="StorageQuotaExceededException">Thrown when storage quota is exhausted or would be exceeded by the upload.</exception>
        private void EnsureWithinQuota(long currentUsedBytes, long incomingBytes)
        {
                long safeUsedBytes = Math.Max(val1: 0L, val2: currentUsedBytes);
                long safeQuotaBytes = Math.Max(val1: 0L, val2: this._options.TotalQuotaBytes);

                // Subtract instead of computing (safeUsedBytes + incomingBytes) so huge values near long.MaxValue cannot wrap negative and bypass quota checks.
                bool exceedsQuota = this._options.TotalQuotaBytes <= 0
                        || safeUsedBytes >= safeQuotaBytes
                        || incomingBytes > safeQuotaBytes - safeUsedBytes;
                if (!exceedsQuota)
                {
                        return;
                }

                string storageNodeId = string.IsNullOrWhiteSpace(this._options.StorageNodeId)
                        ? ApplicationConstants.DEFAULT_STORAGE_NODE_ID
                        : this._options.StorageNodeId.Trim();

                // Saturate projected usage at long.MaxValue so error reporting never throws OverflowException on extreme test values.
                long safeAdditionalBytes = Math.Max(val1: 0L, val2: incomingBytes);
                long projectedUsedBytes = safeAdditionalBytes > long.MaxValue - safeUsedBytes
                        ? long.MaxValue
                        : safeUsedBytes + safeAdditionalBytes;

                throw new StorageQuotaExceededException(
                        storageNodeId: storageNodeId,
                        usedBytes: projectedUsedBytes,
                        quotaBytes: safeQuotaBytes);
        }
}
