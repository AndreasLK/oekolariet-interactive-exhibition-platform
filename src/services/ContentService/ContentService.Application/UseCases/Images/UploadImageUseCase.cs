using System.Buffers;
using System.Security.Cryptography;
using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Validates, hashes, quota-checks, and atomically persists newly uploaded raster exhibition images.
/// </summary>
public sealed class UploadImageUseCase
{
        /// <summary>
        /// Image metadata and atomic binary persistence repository.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Storage node byte consumption monitor.
        /// </summary>
        private readonly IStorageQuotaMonitor _quotaMonitor;

        /// <summary>
        /// Magic-byte header validator preventing MIME spoofing.
        /// </summary>
        private readonly IImageSignatureValidator _signatureValidator;

        /// <summary>
        /// Configured storage node identity, size limits, and quota budget.
        /// </summary>
        private readonly ContentStorageOptions _options;

        /// <summary>
        /// Initializes a new instance of the <see cref="UploadImageUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata and binary persistence repository.</param>
        /// <param name="quotaMonitor">The storage node byte usage monitor.</param>
        /// <param name="signatureValidator">The image magic-byte signature validator.</param>
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
        /// Validates an incoming image upload stream, enforces size/signature/quota rules, and persists metadata and binary atomically.
        /// </summary>
        /// <param name="command">The image upload command carrying stream content, MIME type, file name, and actor audit identity.</param>
        /// <param name="cancellationToken">A token to observe while streaming and persisting the image.</param>
        /// <returns>The persisted <see cref="ImageMetadataResult"/> including generated handle and SHA-256 digest.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="command"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when stream, size, MIME allowlist, magic-byte signature, or entity invariants fail.</exception>
        /// <exception cref="StorageQuotaExceededException">Thrown when the upload would exceed the storage node's configured byte quota.</exception>
        public async Task<ImageMetadataResult> ExecuteAsync(
                UploadImageCommand command,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(command);

                // Run cheap metadata, stream-state, and declared-length checks first to avoid buffering megabytes for invalid requests.
                long effectiveMaxSizeBytes = this.ValidatePreconditions(command: command);

                long currentUsedBytes = await this._quotaMonitor.GetUsedBytesAsync(cancellationToken: cancellationToken);
                this.EnsureWithinQuota(currentUsedBytes: currentUsedBytes, incomingBytes: command.DeclaredLengthBytes);

                // Copy incrementally with a pooled buffer while hashing concurrently so spoofed Content-Length headers cannot cause unbounded reads.
                (byte[] payload, string sha256Hash) = await ReadAndHashPayloadAsync(
                        content: command.Content,
                        maxAllowedBytes: effectiveMaxSizeBytes,
                        cancellationToken: cancellationToken);

                // Cap magic-byte inspection to MAX_MAGIC_HEADER_LENGTH after full buffering so short or truncated streams cannot bypass header checks.
                int headerLength = Math.Min(val1: payload.Length, val2: ApplicationConstants.MAX_MAGIC_HEADER_LENGTH);
                if (!this._signatureValidator.IsValidSignature(
                        headerBytes: payload.AsSpan(start: 0, length: headerLength),
                        declaredContentType: command.ContentType))
                {
                        throw new DomainValidationException(
                                $"Uploaded binary payload does not match declared content type '{command.ContentType}'.");
                }

                // Re-check quota using actual read bytes in case DeclaredLengthBytes understated the real stream length.
                this.EnsureWithinQuota(currentUsedBytes: currentUsedBytes, incomingBytes: payload.Length);

                ImageEntity image = ImageEntity.Create(
                        storageNodeId: this._options.StorageNodeId,
                        fileName: command.FileName,
                        contentType: command.ContentType,
                        fileSizeBytes: payload.Length,
                        sha256Hash: sha256Hash,
                        actorId: command.ActorId,
                        altText: command.AltText);
                ImageBinaryEntity binary = ImageBinaryEntity.Create(imageId: image.Id, data: payload);

                await this._imageRepository.AddAsync(
                        metadata: image,
                        binary: binary,
                        cancellationToken: cancellationToken);

                return ContentResultMapper.ToMetadataResult(image: image);
        }

        /// <summary>
        /// Validates cheap request preconditions and resolves the effective maximum permitted byte limit before buffering stream content.
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

                // Domain enforces an absolute 10 MiB ceiling even if options are configured higher.
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
        /// Validates lightweight metadata strings (file name, actor ID, MIME allowlist, alt text) prior to stream I/O.
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

                // Normalize case and surrounding whitespace before checking the domain MIME allowlist.
                string normalizedContentType = command.ContentType.Trim().ToLowerInvariant();
                if (!DomainConstants.ALLOWED_IMAGE_CONTENT_TYPES.Contains(normalizedContentType))
                {
                        throw new DomainValidationException(
                                $"Content type '{normalizedContentType}' is not an allowed image content type.");
                }

                // AltText is optional, so enforce max length only when non-whitespace text is provided.
                if (!string.IsNullOrWhiteSpace(command.AltText)
                        && command.AltText.Trim().Length > DomainConstants.MAX_ALT_TEXT_LENGTH)
                {
                        throw new DomainValidationException(
                                $"Alternative text cannot exceed {DomainConstants.MAX_ALT_TEXT_LENGTH} characters.");
                }
        }

        /// <summary>
        /// Verifies that adding <paramref name="incomingBytes"/> keeps total node consumption within the configured byte quota.
        /// </summary>
        /// <param name="currentUsedBytes">The current byte usage reported by <see cref="IStorageQuotaMonitor"/>.</param>
        /// <param name="incomingBytes">The byte size of the incoming image payload.</param>
        /// <exception cref="StorageQuotaExceededException">Thrown when storage quota is exhausted or would be exceeded by the upload.</exception>
        private void EnsureWithinQuota(long currentUsedBytes, long incomingBytes)
        {
                long safeUsedBytes = Math.Max(val1: 0L, val2: currentUsedBytes);
                long safeQuotaBytes = Math.Max(val1: 0L, val2: this._options.TotalQuotaBytes);

                // Subtract instead of adding (safeUsedBytes + incomingBytes) so values near long.MaxValue cannot wrap negative.
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

                // Saturate at long.MaxValue instead of overflowing when reporting projected usage in quota errors.
                long safeAdditionalBytes = Math.Max(val1: 0L, val2: incomingBytes);
                long projectedUsedBytes = safeAdditionalBytes > long.MaxValue - safeUsedBytes
                        ? long.MaxValue
                        : safeUsedBytes + safeAdditionalBytes;

                throw new StorageQuotaExceededException(
                        storageNodeId: storageNodeId,
                        usedBytes: projectedUsedBytes,
                        quotaBytes: safeQuotaBytes);
        }

        /// <summary>
        /// Buffers the stream using an 80 KiB pooled buffer while enforcing <paramref name="maxAllowedBytes"/> and computing SHA-256 concurrently.
        /// </summary>
        /// <param name="content">The readable input stream.</param>
        /// <param name="maxAllowedBytes">The maximum permitted actual byte length.</param>
        /// <param name="cancellationToken">A token to observe while reading stream chunks.</param>
        /// <returns>The buffered payload byte array and lowercase 64-character hexadecimal SHA-256 digest.</returns>
        /// <exception cref="DomainValidationException">Thrown when the actual byte count is zero or exceeds <paramref name="maxAllowedBytes"/>.</exception>
        private static async Task<(byte[] Payload, string Sha256Hash)> ReadAndHashPayloadAsync(
                Stream content,
                long maxAllowedBytes,
                CancellationToken cancellationToken)
        {
                using MemoryStream payloadStream = new();
                using IncrementalHash sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(ApplicationConstants.STREAM_BUFFER_SIZE_BYTES);
                long totalBytesRead;

                try
                {
                        totalBytesRead = await CopyAndHashChunksAsync(
                                content: content,
                                payloadStream: payloadStream,
                                sha256: sha256,
                                buffer: buffer,
                                maxAllowedBytes: maxAllowedBytes,
                                cancellationToken: cancellationToken);
                }
                finally
                {
                        // Always return the pooled buffer even if stream reading throws or exceeds size limits.
                        ArrayPool<byte>.Shared.Return(buffer);
                }

                if (totalBytesRead < DomainConstants.MIN_IMAGE_SIZE_BYTES)
                {
                        throw new DomainValidationException(
                                $"Image payload cannot be smaller than {DomainConstants.MIN_IMAGE_SIZE_BYTES} byte(s).");
                }

                return (payloadStream.ToArray(), Convert.ToHexStringLower(sha256.GetHashAndReset()));
        }

        /// <summary>
        /// Streams chunks from <paramref name="content"/> into <paramref name="payloadStream"/> and <paramref name="sha256"/> until EOF or limit breach.
        /// </summary>
        /// <param name="content">The source stream.</param>
        /// <param name="payloadStream">The destination memory buffer.</param>
        /// <param name="sha256">The incremental SHA-256 hasher.</param>
        /// <param name="buffer">The rented read buffer.</param>
        /// <param name="maxAllowedBytes">The maximum permitted total byte count.</param>
        /// <param name="cancellationToken">A token to observe between reads.</param>
        /// <returns>The exact total number of bytes read from <paramref name="content"/>.</returns>
        /// <exception cref="DomainValidationException">Thrown immediately when cumulative bytes exceed <paramref name="maxAllowedBytes"/>.</exception>
        private static async Task<long> CopyAndHashChunksAsync(
                Stream content,
                MemoryStream payloadStream,
                IncrementalHash sha256,
                byte[] buffer,
                long maxAllowedBytes,
                CancellationToken cancellationToken)
        {
                long totalBytesRead = 0;

                while (true)
                {
                        // Bound the read slice to the configured buffer size because ArrayPool.Rent may return a larger array.
                        int bytesRead = await content.ReadAsync(
                                buffer: buffer.AsMemory(start: 0, length: ApplicationConstants.STREAM_BUFFER_SIZE_BYTES),
                                cancellationToken: cancellationToken);
                        if (bytesRead == 0)
                        {
                                return totalBytesRead;
                        }

                        // Abort streaming immediately on overflow so misdeclared or oversized streams cannot exhaust memory.
                        totalBytesRead += bytesRead;
                        if (totalBytesRead > maxAllowedBytes)
                        {
                                throw new DomainValidationException(
                                        $"Image upload exceeds the maximum allowed size of {maxAllowedBytes} bytes.");
                        }

                        // Hash and buffer the exact slice read in one pass to avoid a second pass over the payload.
                        ReadOnlySpan<byte> chunk = buffer.AsSpan(start: 0, length: bytesRead);
                        sha256.AppendData(chunk);
                        payloadStream.Write(chunk);
                }
        }
}

