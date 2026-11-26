using ALKsPersonalUI;
using Bunit;
using ContentService.Components.Components;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ContentService.Tests.Components;

/// <summary>
/// Verifies template preview selection and management visibility in <see cref="ContentTemplateGallery"/>.
/// </summary>
public sealed class ContentTemplateGalleryTests : BunitContext
{
        private const string SAMPLE_SCHEMA_JSON = """
                {
                  "Type": "Text",
                  "Props": { "Value": "Sample Template Content" }
                }
                """;

        /// <summary>
        /// Ensures clicking a template preview in selection mode marks it selected and invokes <see cref="ContentTemplateGallery.OnTemplateSelected"/>.
        /// </summary>
        [Fact]
        public void ContentTemplateGallery_ClickPreviewWhenSelectable_InvokesOnTemplateSelected()
        {
                ContentTemplateResponse template = CreateTemplate(name: "Waste Quiz");
                this.Services.AddSingleton<IContentServiceTemplateClient>(implementationInstance: new StubTemplateClient(templates: [template]));
                this.Services.AddPersonalUILibrary();

                ContentTemplateResponse? selected = null;
                IRenderedComponent<ContentTemplateGallery> cut = this.Render<ContentTemplateGallery>(
                        parameters => parameters.Add(p => p.OnTemplateSelected, item => selected = item));

                cut.WaitForAssertion(
                        assertion: () => Assert.NotNull(@object: cut.Find(cssSelector: ".template-tile-check")));

                cut.Find(cssSelector: ".template-tile-check").Click();

                cut.WaitForAssertion(assertion: () =>
                {
                        Assert.NotNull(@object: selected);
                        Assert.Equal(expected: template.Id, actual: selected.Id);
                        Assert.NotNull(@object: cut.Find(cssSelector: ".template-tile-selected"));
                        Assert.Equal(expected: "true", actual: cut.Find(cssSelector: ".template-tile-check").GetAttribute(name: "aria-pressed"));
                });
        }

        /// <summary>
        /// Ensures management actions are hidden when <see cref="ContentTemplateGallery.AllowManage"/> is <see langword="false"/>.
        /// </summary>
        [Fact]
        public void ContentTemplateGallery_AllowManageFalse_HidesCreateEditAndArchiveButtons()
        {
                ContentTemplateResponse template = CreateTemplate(name: "Read-Only Template");
                this.Services.AddSingleton<IContentServiceTemplateClient>(implementationInstance: new StubTemplateClient(templates: [template]));
                this.Services.AddPersonalUILibrary();

                IRenderedComponent<ContentTemplateGallery> cut = this.Render<ContentTemplateGallery>(
                        parameters => parameters
                                .Add(p => p.AllowManage, false)
                                .Add(p => p.OnTemplateSelected, _ => { }));

                cut.WaitForAssertion(
                        assertion: () => Assert.Contains(expectedSubstring: "Read-Only Template", actualString: cut.Markup, comparisonType: StringComparison.Ordinal));

                Assert.DoesNotContain(expectedSubstring: "New Template", actualString: cut.Markup, comparisonType: StringComparison.Ordinal);
                Assert.DoesNotContain(expectedSubstring: ">Edit<", actualString: cut.Markup, comparisonType: StringComparison.Ordinal);
                Assert.DoesNotContain(expectedSubstring: ">Archive<", actualString: cut.Markup, comparisonType: StringComparison.Ordinal);
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

                public Task<ContentTemplateResponse> CreateTemplateAsync(ContentCreateTemplateCommand command, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentTemplateResponse> UpdateTemplateAsync(Guid templateId, ContentUpdateTemplateCommand command, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task DeleteTemplateAsync(Guid templateId, string? actorId = null, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentImageTemplateReferenceResponse> IsImageReferencedByTemplateAsync(Guid imageId, CancellationToken cancellationToken = default) =>
                        Task.FromResult(result: new ContentImageTemplateReferenceResponse(IsReferenced: false));
        }
}
