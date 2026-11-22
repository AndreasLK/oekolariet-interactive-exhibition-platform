using ContentService.Application.Common;
using ContentService.Domain.Common;
using ContentService.Domain.Exceptions;

namespace ContentService.Application.UseCases.Images;

/// <summary>
/// Reads uploaded image streams in bounded memory chunks so spoofed lengths cannot exhaust server memory.
/// </summary>
internal static class BoundedImageStreamReader
{
        /// <summary>
        /// Buffers <paramref name="content"/> up to <paramref name="maxAllowedBytes"/> and rejects empty or oversized payloads.
        /// </summary>
        /// <param name="content">The readable input stream.</param>
        /// <param name="maxAllowedBytes">The maximum permitted actual byte length.</param>
        /// <param name="cancellationToken">Propagates caller cancellation while reading stream chunks.</param>
        /// <returns>The buffered payload byte array.</returns>
        /// <exception cref="DomainValidationException">Thrown when the actual byte count is zero or exceeds <paramref name="maxAllowedBytes"/>.</exception>
        public static async Task<byte[]> ReadPayloadWithLimitAsync(
                Stream content,
                long maxAllowedBytes,
                CancellationToken cancellationToken)
        {
                using MemoryStream payloadStream = new();
                byte[] buffer = new byte[ApplicationConstants.STREAM_BUFFER_SIZE_BYTES];
                long totalBytesRead = 0;

                while (true)
                {
                        int bytesRead = await content.ReadAsync(
                                buffer: buffer,
                                cancellationToken: cancellationToken);
                        if (bytesRead == 0)
                        {
                                break;
                        }

                        // Abort immediately on overflow so streams lying about Content-Length cannot force gigabyte allocations.
                        totalBytesRead += bytesRead;
                        if (totalBytesRead > maxAllowedBytes)
                        {
                                throw new DomainValidationException(
                                        $"Image upload exceeds the maximum allowed size of {maxAllowedBytes} bytes.");
                        }

                        await payloadStream.WriteAsync(
                                buffer: buffer,
                                offset: 0,
                                count: bytesRead,
                                cancellationToken: cancellationToken);
                }

                // Re-check actual bytes read because empty streams with spoofed non-zero Content-Length would otherwise pass precondition checks.
                if (totalBytesRead < DomainConstants.MIN_IMAGE_SIZE_BYTES)
                {
                        throw new DomainValidationException(
                                $"Image payload cannot be smaller than {DomainConstants.MIN_IMAGE_SIZE_BYTES} byte(s).");
                }

                return payloadStream.ToArray();
        }
}

