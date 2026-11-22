using ALKsPersonalUI.Inputs;
using ALKsPersonalUI.Overlays;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Models;
using Microsoft.Extensions.Options;

namespace ContentService.Components.Internal;

/// <summary>
/// Streams queued <see cref="SelectedImageFile"/> batches sequentially to <see cref="IContentServiceImageClient"/> with bounded WASM memory usage.
/// </summary>
internal static class ContentImageBatchUploader
{
        private const int BYTES_PER_KIBIBYTE = 1024;
        private const int BYTES_PER_MEBIBYTE = BYTES_PER_KIBIBYTE * BYTES_PER_KIBIBYTE;

        /// <summary>
        /// Must match ContentService's 10 MiB max image upload limit; Blazor's default <c>OpenReadStream</c> throws at 512 KiB without this.
        /// </summary>
        public const long MAX_IMAGE_SIZE_BYTES = 10L * BYTES_PER_MEBIBYTE;

        /// <summary>
        /// Caps folder-picker selections so reading/uploading streams sequentially never freezes or OOMs Blazor WASM.
        /// </summary>
        public const int MAX_FILES_PER_BATCH = 50;

        /// <summary>
        /// Uploads queued files sequentially so browser WASM memory only holds one active file stream at a time.
        /// </summary>
        public static async Task<(List<ContentImageMetadata> Uploaded, int FailedCount)> UploadBatchAsync(
                IContentServiceImageClient imageClient,
                ToastService toasts,
                IReadOnlyList<SelectedImageFile> files,
                string? baseAlbumPath,
                string altText,
                string tags)
        {
                List<ContentImageMetadata> uploaded = [];
                int failedCount = 0;

                foreach (SelectedImageFile selectedFile in files)
                {
                        ContentImageMetadata? created = await TryUploadSingleFileAsync(
                                imageClient: imageClient,
                                toasts: toasts,
                                selectedFile: selectedFile,
                                baseAlbumPath: baseAlbumPath,
                                altText: altText,
                                tags: tags);

                        if (created is not null)
                        {
                                uploaded.Add(item: created);
                                continue;
                        }

                        failedCount++;
                }

                return (uploaded, failedCount);
        }

        private static async Task<ContentImageMetadata?> TryUploadSingleFileAsync(
                IContentServiceImageClient imageClient,
                ToastService toasts,
                SelectedImageFile selectedFile,
                string? baseAlbumPath,
                string altText,
                string tags)
        {
                string fileName = selectedFile.File.Name;

                try
                {
                        // Pass ContentService's 10 MiB limit explicitly: Blazor's IBrowserFile.OpenReadStream throws IOException above 512 KiB by default.
                        // await using ensures the browser JS-interop file stream is disposed immediately after each upload completes.
                        await using Stream payloadStream = selectedFile.File.OpenReadStream(maxAllowedSize: MAX_IMAGE_SIZE_BYTES);

                        // Normalize browser MIME quirks (e.g., "image/jpg" or blank/octet-stream for .webp) before server-side magic-byte checks.
                        string normalizedMime = ImageContentTypeNormalizer.Normalize(
                                rawContentType: selectedFile.File.ContentType,
                                fileName: fileName);

                        UploadImageRequest request = new()
                        {
                                Content = payloadStream,
                                FileName = fileName,
                                ContentType = normalizedMime,
                                AltText = string.IsNullOrWhiteSpace(value: altText) ? null : altText.Trim(),
                                AlbumPath = ResolveTargetAlbumPath(baseAlbumPath: baseAlbumPath, selectedFile: selectedFile),
                                Tags = string.IsNullOrWhiteSpace(value: tags) ? null : tags.Trim()
                        };

                        return await imageClient.UploadImageAsync(request: request);
                }
                catch (IOException ex)
                {
                        // Typically triggered when the selected file exceeds MAX_IMAGE_SIZE_BYTES (10 MiB) or the browser lost access to the file handle.
                        toasts.ShowDanger(messageText: $"{fileName}: {ex.Message}");
                        return null;
                }
                catch (ContentServiceApiException ex)
                {
                        // Server rejected the upload (e.g., 415 spoofed magic bytes, 422 validation error, or 507 storage quota exceeded).
                        toasts.ShowDanger(messageText: $"{fileName}: {ex.ProblemDetails?.Detail ?? ex.Message}");
                        return null;
                }
                catch (HttpRequestException ex)
                {
                        toasts.ShowDanger(messageText: $"{fileName}: Network error - {ex.Message}");
                        return null;
                }
                catch (Exception ex) when (ex is InvalidOperationException or OptionsValidationException)
                {
                        // Triggered when ContentService SDK BaseAddress or client options are missing/invalid.
                        toasts.ShowDanger(messageText: $"{fileName}: {ex.Message}");
                        return null;
                }
        }

        /// <summary>
        /// Merges the base album path with any folder-upload subdirectory prefix extracted from <see cref="SelectedImageFile.RelativePath"/>.
        /// </summary>
        public static string? ResolveTargetAlbumPath(
                string? baseAlbumPath,
                SelectedImageFile selectedFile)
        {
                string? relativeSubfolder = AlbumPathNormalizer.ExtractRelativeDirectory(relativeFilePath: selectedFile.RelativePath);
                return AlbumPathNormalizer.Combine(
                        baseAlbumPath: baseAlbumPath,
                        subAlbumPath: relativeSubfolder);
        }
}
