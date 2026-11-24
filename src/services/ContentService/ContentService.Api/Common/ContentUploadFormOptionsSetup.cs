using ContentService.Application.Options;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

namespace ContentService.Api.Common;

/// <summary>
/// Aligns ASP.NET Core multipart parsing limits with <see cref="ContentStorageOptions.MaxImageSizeBytes"/> so valid uploads are not rejected prematurely and huge payloads cannot exhaust memory.
/// </summary>
internal sealed class ContentUploadFormOptionsSetup : IConfigureOptions<FormOptions>
{
        /// <summary>
        /// Storage options providing the authoritative maximum image byte limit.
        /// </summary>
        private readonly IOptions<ContentStorageOptions> _storageOptions;

        /// <summary>
        /// Captures validated storage options so multipart limits update automatically whenever host configuration changes.
        /// </summary>
        /// <param name="storageOptions">The configured ContentService storage options.</param>
        public ContentUploadFormOptionsSetup(IOptions<ContentStorageOptions> storageOptions)
        {
                ArgumentNullException.ThrowIfNull(storageOptions);
                this._storageOptions = storageOptions;
        }

        /// <inheritdoc />
        public void Configure(FormOptions options)
        {
                ArgumentNullException.ThrowIfNull(options);

                // Add multipart framing overhead above MaxImageSizeBytes so valid max-size images are never blocked by form boundary bytes before reaching UploadImageUseCase.
                long maxImageBytes = Math.Max(val1: 0L, val2: this._storageOptions.Value.MaxImageSizeBytes);
                options.MultipartBodyLengthLimit = maxImageBytes + ApiConstants.MULTIPART_ENVELOPE_OVERHEAD_BYTES;
        }
}

