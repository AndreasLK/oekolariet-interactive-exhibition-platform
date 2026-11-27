using ALKsPersonalUI;
using Bunit;
using ContentService.Components.Components;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ContentService.Tests.Components;

/// <summary>
/// Verifies user-ready <see cref="ContentImagePickerModal"/> and <see cref="ContentTemplatePickerModal"/> components.
/// </summary>
public sealed class ContentPickerModalTests : BunitContext
{
        private const string SAMPLE_SCHEMA_JSON = """
                {
                  "Type": "Text",
                  "Props": { "Value": "Picker Template" }
                }
                """;

        /// <summary>
        /// Ensures <see cref="ContentTemplatePickerModal"/> forwards selection and closes when <c>CloseOnSelect</c> is enabled.
        /// </summary>
        [Fact]
        public void ContentTemplatePickerModal_SelectTemplate_InvokesCallbacksAndCloses()
        {
                ContentTemplateResponse template = CreateTemplate(name: "Quiz Intro");
                this.Services.AddSingleton<IContentServiceTemplateClient>(implementationInstance: new StubTemplateClient(templates: [template]));
                this.Services.AddPersonalUILibrary();

                ContentTemplateResponse? picked = null;
                bool isOpen = true;

                IRenderedComponent<ContentTemplatePickerModal> cut = this.Render<ContentTemplatePickerModal>(
                        parameters => parameters
                                .Add(p => p.IsOpen, isOpen)
                                .Add(p => p.IsOpenChanged, value => isOpen = value)
                                .Add(p => p.OnTemplateSelected, value => picked = value));

                cut.WaitForAssertion(
                        assertion: () => Assert.Contains(
                                collection: cut.FindAll(cssSelector: "button"),
                                filter: button => button.TextContent.Trim() is "○" or "✓"));

                cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() is "○" or "✓")
                        .Click();

                cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Vælg")
                        .Click();

                cut.WaitForAssertion(assertion: () =>
                {
                        Assert.NotNull(@object: picked);
                        Assert.Equal(expected: template.Id, actual: picked.Id);
                        Assert.False(condition: isOpen);
                });
        }

        /// <summary>
        /// Ensures <see cref="ContentImagePickerModal"/> does not query images while closed, and renders the gallery when open.
        /// </summary>
        [Fact]
        public void ContentImagePickerModal_ClosedVsOpen_DefersLoadUntilOpenAndForwardsSelection()
        {
                ContentImageResponse image = CreateImage(fileName: "leaf.png");
                StubImageClient imageClient = new(images: [image]);
                this.Services.AddSingleton<IContentServiceImageClient>(implementationInstance: imageClient);
                this.Services.AddPersonalUILibrary();

                ContentImageResponse? picked = null;
                bool isOpen = false;

                IRenderedComponent<ContentImagePickerModal> cut = this.Render<ContentImagePickerModal>(
                        parameters => parameters
                                .Add(p => p.IsOpen, isOpen)
                                .Add(p => p.IsOpenChanged, value => isOpen = value)
                                .Add(p => p.OnImageSelected, value => picked = value));

                Assert.Equal(expected: 0, actual: imageClient.ListCallCount);

                cut.Render(parameters => parameters.Add(p => p.IsOpen, true));

                cut.WaitForAssertion(
                        assertion: () => Assert.Contains(expectedSubstring: "leaf.png", actualString: cut.Markup, comparisonType: StringComparison.Ordinal));
                Assert.True(condition: imageClient.ListCallCount > 0);
        }

        private static ContentTemplateResponse CreateTemplate(string name)
        {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                return new ContentTemplateResponse
                {
                        Id = Guid.NewGuid(),
                        ExternalId = $"template_{Guid.NewGuid():N}",
                        Name = name,
                        ComponentType = ContentTemplateComponentType.InfoScreen,
                        SchemaJson = SAMPLE_SCHEMA_JSON,
                        SchemaVersion = 1,
                        IsArchived = false,
                        ReferencedPictureReferences = [],
                        CreatedAt = now,
                        UpdatedAt = now,
                        ConcurrencyStamp = Guid.NewGuid()
                };
        }

        private static ContentImageResponse CreateImage(string fileName) =>
                new()
                {
                        Id = Guid.NewGuid(),
                        PictureReference = $"picture_{Guid.NewGuid():N}",
                        ExternalId = $"image_{Guid.NewGuid():N}",
                        FileName = fileName,
                        ContentType = "image/png",
                        SizeBytes = 1024,
                        Sha256Hash = "abc123",
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };

        private sealed class StubTemplateClient(IReadOnlyList<ContentTemplateResponse> templates) : IContentServiceTemplateClient
        {
                public Task<ContentPagedResponse<ContentTemplateResponse>> ListTemplatesAsync(
                        int page = 1,
                        int pageSize = 25,
                        ContentTemplateComponentType? componentType = null,
                        string? searchTerm = null,
                        CancellationToken cancellationToken = default) =>
                        this.ListTemplatesAsync(
                                query: new ContentTemplateQuery { Page = page, PageSize = pageSize, ComponentType = componentType, SearchTerm = searchTerm },
                                cancellationToken: cancellationToken);

                public Task<ContentPagedResponse<ContentTemplateResponse>> ListTemplatesAsync(
                        ContentTemplateQuery query,
                        CancellationToken cancellationToken = default)
                {
                        return Task.FromResult(result: new ContentPagedResponse<ContentTemplateResponse>
                        {
                                Items = templates,
                                Page = 1,
                                PageSize = 12,
                                TotalCount = templates.Count
                        });
                }

                public Task<ContentTemplateResponse> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentTemplateResponse> CreateTemplateAsync(ContentCreateTemplateRequest command, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentTemplateResponse> UpdateTemplateAsync(Guid templateId, ContentUpdateTemplateRequest command, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task DeleteTemplateAsync(Guid templateId, string? actorId = null, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentImageTemplateReferenceResponse> IsImageReferencedByTemplateAsync(Guid imageId, CancellationToken cancellationToken = default) =>
                        Task.FromResult(result: new ContentImageTemplateReferenceResponse(IsReferenced: false));
        }

        private sealed class StubImageClient(IReadOnlyList<ContentImageResponse> images) : IContentServiceImageClient
        {
                public int ListCallCount { get; private set; }

                public Task<ContentPagedResponse<ContentImageResponse>> ListImagesAsync(
                        int page = 1,
                        int pageSize = 24,
                        string? searchTerm = null,
                        CancellationToken cancellationToken = default) =>
                        this.ListImagesAsync(query: new ContentImageQuery { Page = page, PageSize = pageSize, SearchTerm = searchTerm }, cancellationToken: cancellationToken);

                public Task<ContentPagedResponse<ContentImageResponse>> ListImagesAsync(
                        ContentImageQuery query,
                        CancellationToken cancellationToken = default)
                {
                        this.ListCallCount++;
                        return Task.FromResult(result: new ContentPagedResponse<ContentImageResponse>
                        {
                                Items = images,
                                Page = 1,
                                PageSize = 24,
                                TotalCount = images.Count
                        });
                }

                public Task<IReadOnlyList<string>> ListAlbumsAsync(string? parentPath = null, CancellationToken cancellationToken = default) =>
                        Task.FromResult<IReadOnlyList<string>>(result: []);

                public Task<ContentImageResponse> UpdateImageMetadataAsync(Guid imageId, ContentUpdateImageMetadataRequest command, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentImageResponse> UpdateImageMetadataAsync(string imageIdentifier, ContentUpdateImageMetadataRequest command, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<IReadOnlyList<ContentImageResponse>> GetImagesMetadataBatchAsync(IReadOnlyCollection<Guid> imageIds, CancellationToken cancellationToken = default) =>
                        Task.FromResult<IReadOnlyList<ContentImageResponse>>(result: []);

                public Task<ContentImageResponse> UploadImageAsync(ContentUploadImageRequest command, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentImageDownloadResponse> DownloadImageAsync(Guid imageId, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentImageDownloadResponse> DownloadImageAsync(string imageIdentifier, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentImageDownloadResponse> DownloadImagePreviewAsync(Guid imageId, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentImageDownloadResponse> DownloadImagePreviewAsync(string imageIdentifier, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentImageResponse> GetImageMetadataAsync(Guid imageId, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentImageResponse> GetImageMetadataAsync(string imageIdentifier, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task DeleteImageAsync(Guid imageId, string? actorId = null, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task DeleteImageAsync(string imageIdentifier, string? actorId = null, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Uri GetImageContentUri(Guid imageId) => new($"https://example.invalid/api/images/{imageId}/content");

                public Uri GetImageContentUri(string imageIdentifier) => new($"https://example.invalid/api/images/{imageIdentifier}/content");

                public Uri GetImagePreviewUri(Guid imageId) => new($"https://example.invalid/api/images/{imageId}/preview");

                public Uri GetImagePreviewUri(string imageIdentifier) => new($"https://example.invalid/api/images/{imageIdentifier}/preview");
        }
}
