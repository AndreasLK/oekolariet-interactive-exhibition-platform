using ALKsPersonalUI;
using ALKsPersonalUI.Overlays;
using Bunit;
using ContentService.Components.Components;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Tests.Components;

/// <summary>
/// bUnit component tests verifying pagination navigation and filter page resets in <see cref="ContentTemplateGallery"/>.
/// </summary>
public sealed class ContentTemplateGalleryTests : BunitContext
{
        private const int TEST_PAGE_SIZE = 2;
        private const int TOTAL_TEMPLATE_COUNT = 3;
        private const int FIRST_PAGE = 1;
        private const int SECOND_PAGE = 2;

        /// <summary>
        /// Verifies that clicking Næste requests page 2, updates the page indicator, and resets to page 1 when filtering.
        /// </summary>
        [Fact]
        public void Pagination_WhenNextClickedAndFilterChanged_NavigatesToPageTwoAndResetsToPageOne()
        {
                // Arrange
                List<ContentTemplateQuery> recordedQueries = [];
                ContentTemplateMetadata first = CreateTemplate(name: "Template Alpha");
                ContentTemplateMetadata second = CreateTemplate(name: "Template Beta");
                ContentTemplateMetadata third = CreateTemplate(name: "Template Gamma");

                this.JSInterop.Mode = JSRuntimeMode.Loose;
                this.Services.AddPersonalUILibrary(configure: static _ => { });
                this.Services.AddScoped<ToastService>();
                this.Services.AddSingleton<IContentServiceTemplateClient>(
                        implementationInstance: new PagedStubTemplateClient(
                                allTemplates: [first, second, third],
                                recordedQueries: recordedQueries));

                IRenderedComponent<ContentTemplateGallery> cut = this.Render<ContentTemplateGallery>(
                        parameterBuilder: parameters => parameters.Add(parameterSelector: component => component.PageSize, value: TEST_PAGE_SIZE));

                Assert.Contains(expectedSubstring: "Side 1 af 2", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "Template Alpha", actualString: cut.Markup);
                Assert.DoesNotContain(expectedSubstring: "Template Gamma", actualString: cut.Markup);

                // Act 1: navigate to page 2
                AngleSharp.Dom.IElement nextButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Næste");
                nextButton.Click();

                // Assert 1: page 2 is requested and rendered
                Assert.Equal(expected: SECOND_PAGE, actual: recordedQueries[^1].Page);
                Assert.Contains(expectedSubstring: "Side 2 af 2", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "Template Gamma", actualString: cut.Markup);

                // Act 2: switch component type filter while on page 2
                AngleSharp.Dom.IElement filterPill = cut.FindAll(cssSelector: "button, span, div")
                        .First(predicate: element => element.TextContent.Trim() == ContentTemplateComponentType.InfoScreen.ToString());
                filterPill.Click();

                // Assert 2: query resets to page 1
                Assert.Equal(expected: FIRST_PAGE, actual: recordedQueries[^1].Page);
                Assert.Equal(expected: ContentTemplateComponentType.InfoScreen, actual: recordedQueries[^1].ComponentType);
        }

        private static ContentTemplateMetadata CreateTemplate(string name) => new()
        {
                Id = Guid.NewGuid(),
                ExternalId = $"tpl-{name.ToLowerInvariant().Replace(oldValue: " ", newValue: "-")}",
                Name = name,
                ComponentType = ContentTemplateComponentType.InfoScreen,
                SchemaVersion = 1,
                SchemaJson = "{\"Type\":\"Column\",\"Children\":[]}",
                IsArchived = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                ConcurrencyStamp = Guid.NewGuid()
        };

        private sealed class PagedStubTemplateClient(
                IReadOnlyList<ContentTemplateMetadata> allTemplates,
                List<ContentTemplateQuery> recordedQueries) : IContentServiceTemplateClient
        {
                public Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
                        int page = 1,
                        int pageSize = 25,
                        ContentTemplateComponentType? componentType = null,
                        string? searchTerm = null,
                        CancellationToken cancellationToken = default) =>
                        this.ListTemplatesAsync(
                                query: new ContentTemplateQuery
                                {
                                        Page = page,
                                        PageSize = pageSize,
                                        ComponentType = componentType,
                                        SearchTerm = searchTerm
                                },
                                cancellationToken: cancellationToken);

                public Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
                        ContentTemplateQuery query,
                        CancellationToken cancellationToken = default)
                {
                        recordedQueries.Add(item: query);
                        List<ContentTemplateMetadata> pageItems = allTemplates
                                .Skip(count: (query.Page - 1) * query.PageSize)
                                .Take(count: query.PageSize)
                                .ToList();

                        return Task.FromResult(result: new ContentPagedResult<ContentTemplateMetadata>
                        {
                                Items = pageItems,
                                Page = query.Page,
                                PageSize = query.PageSize,
                                TotalCount = TOTAL_TEMPLATE_COUNT
                        });
                }

                public Task<ContentTemplateMetadata> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentTemplateMetadata> CreateTemplateAsync(CreateContentTemplateRequest request, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentTemplateMetadata> UpdateTemplateAsync(Guid templateId, UpdateContentTemplateRequest request, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task DeleteTemplateAsync(Guid templateId, string? actorId = null, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<bool> IsImageReferencedByTemplateAsync(Guid imageId, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();
        }
}
