using ContentService.Application.Interfaces;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Domain.Entities;
using ContentService.Domain.Exceptions;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Opens a sequential binary stream for an exhibition image, skipping storage reads when the client's ETag matches.
/// </summary>
public sealed class GetImageStreamUseCase
{
        /// <summary>
        /// Image metadata repository.
        /// </summary>
        private readonly IImageRepository _imageRepository;

        /// <summary>
        /// Sequential binary stream store.
        /// </summary>
        private readonly IImageBinaryStore _imageBinaryStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetImageStreamUseCase"/> class.
        /// </summary>
        /// <param name="imageRepository">The image metadata repository.</param>
        /// <param name="imageBinaryStore">The sequential binary stream store.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
        public GetImageStreamUseCase(
                IImageRepository imageRepository,
                IImageBinaryStore imageBinaryStore)
        {
                ArgumentNullException.ThrowIfNull(imageRepository);
                ArgumentNullException.ThrowIfNull(imageBinaryStore);

                this._imageRepository = imageRepository;
                this._imageBinaryStore = imageBinaryStore;
        }

        /// <summary>
        /// Resolves metadata, evaluates optional <c>If-None-Match</c> ETag caching, and opens the image binary stream on cache miss.
        /// </summary>
        /// <param name="query">The image stream query containing the image ID and optional client ETag header.</param>
        /// <param name="cancellationToken">A token to observe while loading metadata or opening the stream.</param>
        /// <returns>An <see cref="ImageStreamResult"/> carrying either a not-modified marker or an open stream owned by the caller.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="query"/> is <see langword="null"/>.</exception>
        /// <exception cref="DomainValidationException">Thrown when <see cref="GetImageStreamQuery.ImageId"/> is <see cref="Guid.Empty"/>.</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the image is missing, soft-deleted, or its binary payload cannot be opened.</exception>
        public async Task<ImageStreamResult> ExecuteAsync(
                GetImageStreamQuery query,
                CancellationToken cancellationToken)
        {
                ArgumentNullException.ThrowIfNull(query);

                if (query.ImageId == Guid.Empty)
                {
                        throw new DomainValidationException("Image ID cannot be empty.");
                }

                ImageEntity? image = await this._imageRepository.GetByIdAsync(
                        id: query.ImageId,
                        cancellationToken: cancellationToken);
                // Treat soft-deleted images as missing so deleted binaries cannot be streamed or probed.
                if (image is null || image.IsDeleted)
                {
                        throw new KeyNotFoundException($"Image '{query.ImageId}' was not found.");
                }

                ImageMetadataResult metadata = ContentResultMapper.ToMetadataResult(image: image);

                // Compare against metadata.Sha256Hash first so HTTP 304 cache hits never touch IImageBinaryStore.
                if (IsMatchingETag(ifNoneMatchETag: query.IfNoneMatchETag, expectedSha256Hash: image.Sha256Hash))
                {
                        return ImageStreamResult.NotModified(metadata: metadata);
                }

                Stream? contentStream = await this._imageBinaryStore.OpenReadAsync(
                        imageId: image.Id,
                        cancellationToken: cancellationToken);
                if (contentStream is null)
                {
                        throw new KeyNotFoundException($"Binary payload for image '{query.ImageId}' was not found.");
                }

                // Transfer stream ownership to the result, disposing immediately if wrapping fails before returning to the caller.
                try
                {
                        return ImageStreamResult.WithContent(metadata: metadata, content: contentStream);
                }
                catch
                {
                        await contentStream.DisposeAsync();
                        throw;
                }
        }

        /// <summary>
        /// Parses an <c>If-None-Match</c> token or comma-separated ETag list and compares normalized tags against the stored SHA-256 digest.
        /// </summary>
        /// <param name="ifNoneMatchETag">The raw client <c>If-None-Match</c> value, or <see langword="null"/>.</param>
        /// <param name="expectedSha256Hash">The image's stored 64-character SHA-256 digest.</param>
        /// <returns><see langword="true"/> if any candidate tag matches <paramref name="expectedSha256Hash"/>; otherwise <see langword="false"/>.</returns>
        private static bool IsMatchingETag(string? ifNoneMatchETag, string expectedSha256Hash)
        {
                // Treat absent or blank If-None-Match headers as a cache miss.
                if (string.IsNullOrWhiteSpace(ifNoneMatchETag))
                {
                        return false;
                }

                // Split comma-separated ETag lists per RFC 9110 so multi-tag conditional requests match any valid tag.
                string[] candidateTokens = ifNoneMatchETag.Split(
                        separator: ApplicationConstants.ETAG_LIST_SEPARATOR,
                        options: StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                foreach (string rawToken in candidateTokens)
                {
                        ReadOnlySpan<char> normalized = NormalizeETagToken(rawToken: rawToken);

                        // Compare case-insensitively because stored SHA-256 digests are lowercase hex while clients may send uppercase.
                        if (normalized.Equals(other: expectedSha256Hash.AsSpan(), comparisonType: StringComparison.OrdinalIgnoreCase))
                        {
                                return true;
                        }
                }

                return false;
        }

        /// <summary>
        /// Strips an optional RFC 9110 weak prefix (<c>W/</c>) and matching enclosing double quotes safely.
        /// </summary>
        /// <param name="rawToken">A trimmed single ETag candidate token.</param>
        /// <returns>The unwrapped tag characters, or the original span if not symmetrically quoted.</returns>
        private static ReadOnlySpan<char> NormalizeETagToken(string rawToken)
        {
                ReadOnlySpan<char> candidate = rawToken.AsSpan();

                // Strip RFC 9110 case-sensitive "W/" weak indicator before checking surrounding quotes.
                if (candidate.StartsWith(value: ApplicationConstants.WEAK_ETAG_PREFIX, comparisonType: StringComparison.Ordinal))
                {
                        candidate = candidate[ApplicationConstants.WEAK_ETAG_PREFIX.Length..];
                }

                // Require balanced outer quotes of length >= 2 so malformed inputs like "\"" or "\"abc" never throw or silently match.
                if (candidate.Length >= ApplicationConstants.MINIMUM_QUOTED_ETAG_LENGTH
                        && candidate[0] == ApplicationConstants.ETAG_QUOTE_CHAR
                        && candidate[^1] == ApplicationConstants.ETAG_QUOTE_CHAR)
                {
                        return candidate[1..^1];
                }

                return candidate;
        }
}

