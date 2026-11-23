using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.UseCases.Images;
using ContentService.Domain.Entities;
using ContentService.Infrastructure.Images;
using ContentService.Tests.TestDoubles;
using SkiaSharp;
using Xunit;

namespace ContentService.Tests.Application;

/// <summary>
/// Verifies saved image preview generation and lazy backfill in <see cref="GetImagePreviewUseCase"/> and <see cref="SkiaImagePreviewGenerator"/>.
/// </summary>
public sealed class ImagePreviewTests
{
        private const int LARGE_DIMENSION_PX = 800;
        private const int BOUNDED_PREVIEW_MAX_PX = 400;

        /// <summary>
        /// Ensures <see cref="SkiaImagePreviewGenerator"/> downscales oversized images to bounded WebP previews.
        /// </summary>
        [Fact]
        public async Task TryGeneratePreviewAsync_OversizedPng_ReturnsBoundedWebpPreview()
        {
                byte[] sourcePng = CreateTestPngBytes(width: LARGE_DIMENSION_PX, height: LARGE_DIMENSION_PX / 2);
                SkiaImagePreviewGenerator generator = new();

                ImagePreviewPayload? preview = await generator.TryGeneratePreviewAsync(
                        sourceBytes: sourcePng,
                        cancellationToken: TestContext.Current.CancellationToken);

                Assert.NotNull(preview);
                Assert.Equal(expected: "image/webp", actual: preview.ContentType);

                using SKBitmap? decoded = SKBitmap.Decode(buffer: preview.Bytes);
                Assert.NotNull(decoded);
                Assert.Equal(expected: BOUNDED_PREVIEW_MAX_PX, actual: decoded.Width);
                Assert.Equal(expected: BOUNDED_PREVIEW_MAX_PX / 2, actual: decoded.Height);
        }

        /// <summary>
        /// Ensures <see cref="GetImagePreviewUseCase"/> serves an existing saved preview without re-reading or re-encoding the original payload.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_WhenPreviewAlreadySaved_ServesSavedPreviewDirectly()
        {
                FakeImageRepository repository = new();
                ImageEntity image = ImageEntity.Create(
                        storageNodeId: "node-a",
                        fileName: "photo.png",
                        contentType: "image/png",
                        fileSizeBytes: 2048,
                        sha256Hash: new string(c: 'a', count: 64),
                        actorId: "tester");
                await repository.AddAsync(
                        metadata: image,
                        binary: ImageBinaryEntity.Create(imageId: image.Id, data: [1, 2, 3, 4]),
                        cancellationToken: TestContext.Current.CancellationToken);

                InMemoryPreviewBinaryStore store = new(savedPreview: new ImagePreviewPayload(Bytes: [9, 8, 7], ContentType: "image/webp"));
                GetImagePreviewUseCase useCase = new(imageRepository: repository, imageBinaryStore: store);

                await using ImageStreamResult result = await useCase.ExecuteAsync(
                        query: new GetImageStreamQuery { ImageId = image.Id },
                        cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(expected: "image/webp", actual: result.Metadata.ContentType);
                Assert.Equal(expected: 3, actual: result.Metadata.SizeBytes);
                Assert.Equal(expected: 0, actual: store.OriginalReadCount);
        }

        private static byte[] CreateTestPngBytes(int width, int height)
        {
                using SKBitmap bitmap = new(width: width, height: height);
                using SKCanvas canvas = new(bitmap: bitmap);
                canvas.Clear(color: SKColors.Teal);

                using SKImage image = SKImage.FromBitmap(bitmap: bitmap);
                using SKData encoded = image.Encode(format: SKEncodedImageFormat.Png, quality: 100);
                return encoded.ToArray();
        }

        private sealed class InMemoryPreviewBinaryStore : IImageBinaryStore
        {
                private readonly ImagePreviewPayload? _savedPreview;

                public InMemoryPreviewBinaryStore(ImagePreviewPayload? savedPreview)
                {
                        this._savedPreview = savedPreview;
                }

                public int OriginalReadCount { get; private set; }

                public Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken)
                {
                        this.OriginalReadCount++;
                        return Task.FromResult<Stream?>(result: new MemoryStream(buffer: [1, 2, 3, 4], writable: false));
                }

                public Task<ImagePreviewStreamResult?> OpenPreviewReadAsync(Guid imageId, CancellationToken cancellationToken)
                {
                        if (this._savedPreview is null)
                        {
                                return Task.FromResult<ImagePreviewStreamResult?>(result: null);
                        }

                        return Task.FromResult<ImagePreviewStreamResult?>(result: new ImagePreviewStreamResult(
                                Content: new MemoryStream(buffer: this._savedPreview.Bytes, writable: false),
                                ContentType: this._savedPreview.ContentType,
                                SizeBytes: this._savedPreview.Bytes.Length));
                }
        }
}

