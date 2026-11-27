using ALKsPersonalUI;
using ALKsPersonalUI.Overlays;
using Bunit;
using ContentService.Components.Components;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using ContentService.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ContentService.Tests.Components;

/// <summary>
/// bUnit component tests verifying catalog rendering, recursive album navigation, search debouncing, and error handling in <see cref="ContentImageGallery"/>.
/// </summary>
public sealed class ContentImageGalleryTests : BunitContext
{
        private const int TEST_PAGE_SIZE = 2;
        private const int SEARCH_DEBOUNCE_WAIT_MS = 400;

        /// <summary>
        /// Verifies that initial gallery render displays fetched image tiles and the total image count indicator.
        /// </summary>
        [Fact]
        public void InitialRender_WhenImagesExist_DisplaysMediaTilesAndCount()
        {
                // Arrange
                ContentImageResponse first = CreateMetadata(fileName: "forest.png");
                ContentImageResponse second = CreateMetadata(fileName: "ocean.jpg");
                StubContentImageClient stubClient = new()
                {
                        OnListImagesQueryAsync = (query, _) => Task.FromResult(result: new ContentPagedResponse<ContentImageResponse>
                        {
                                Items = [first, second],
                                Page = query.Page,
                                PageSize = TEST_PAGE_SIZE,
                                TotalCount = 2
                        })
                };
                this.RegisterUiServices(imageClient: stubClient);

                // Act
                IRenderedComponent<ContentImageGallery> cut = this.Render<ContentImageGallery>(
                        parameterBuilder: parameters => parameters.Add(parameterSelector: component => component.PageSize, value: TEST_PAGE_SIZE));

                // Assert
                Assert.Contains(expectedSubstring: "2 billede(r)", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "forest.png", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "ocean.jpg", actualString: cut.Markup);
        }

        /// <summary>
        /// Verifies that <c>DefaultAlbumPath</c> and <c>LockToDefaultAlbum</c> scope queries to the host album and render child album pills.
        /// </summary>
        [Fact]
        public void DefaultAlbumPath_WhenLocked_ScopesQueryAndNavigatesChildAlbums()
        {
                // Arrange
                List<string?> queriedAlbums = [];
                ContentImageResponse banana = CreateMetadata(fileName: "banana.png", albumPath: "Madaffald");
                StubContentImageClient stubClient = new()
                {
                        OnListImagesQueryAsync = (query, _) =>
                        {
                                queriedAlbums.Add(item: query.AlbumPath);
                                return Task.FromResult(result: new ContentPagedResponse<ContentImageResponse>
                                {
                                        Items = [banana],
                                        Page = 1,
                                        PageSize = TEST_PAGE_SIZE,
                                        TotalCount = 1
                                });
                        },
                        OnListAlbumsAsync = (parentPath, _) =>
                        {
                                IReadOnlyList<string> children = parentPath == "Madaffald"
                                        ? ["Madaffald/Frugt"]
                                        : [];
                                return Task.FromResult(result: children);
                        }
                };
                this.RegisterUiServices(imageClient: stubClient);

                // Act
                IRenderedComponent<ContentImageGallery> cut = this.Render<ContentImageGallery>(
                        parameterBuilder: parameters => parameters
                                .Add(parameterSelector: component => component.DefaultAlbumPath, value: "Madaffald")
                                .Add(parameterSelector: component => component.LockToDefaultAlbum, value: true)
                                .Add(parameterSelector: component => component.PageSize, value: TEST_PAGE_SIZE));

                // Assert locked root omits All Images and shows child album pill
                Assert.DoesNotContain(expectedSubstring: "All Images", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "Madaffald", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "Frugt", actualString: cut.Markup);

                // Act: navigate into child album Frugt
                AngleSharp.Dom.IElement childPill = cut.FindAll(cssSelector: "button, span, div")
                        .First(predicate: element => element.TextContent.Trim() == "Frugt");
                childPill.Click();

                // Assert
                cut.WaitForAssertion(assertion: () =>
                {
                        Assert.Contains(expected: "Madaffald", collection: queriedAlbums);
                        Assert.Contains(expected: "Madaffald/Frugt", collection: queriedAlbums);
                });
        }

        /// <summary>
        /// Verifies that typing into the search input debounces and queries <see cref="IContentServiceImageClient.ListImagesAsync(ContentImageQuery, CancellationToken)"/> with the search term.
        /// </summary>
        [Fact]
        public async Task SearchInput_WhenChanged_DebouncesAndQueriesFilteredResultsAsync()
        {
                // Arrange
                List<string?> recordedSearches = [];
                ContentImageResponse match = CreateMetadata(fileName: "whale.webp");
                StubContentImageClient stubClient = new()
                {
                        OnListImagesQueryAsync = (query, _) =>
                        {
                                recordedSearches.Add(item: query.SearchTerm);
                                IReadOnlyList<ContentImageResponse> items = string.IsNullOrEmpty(value: query.SearchTerm) ? [] : [match];
                                return Task.FromResult(result: new ContentPagedResponse<ContentImageResponse>
                                {
                                        Items = items,
                                        Page = 1,
                                        PageSize = TEST_PAGE_SIZE,
                                        TotalCount = items.Count
                                });
                        }
                };
                this.RegisterUiServices(imageClient: stubClient);

                IRenderedComponent<ContentImageGallery> cut = this.Render<ContentImageGallery>(
                        parameterBuilder: parameters => parameters.Add(parameterSelector: component => component.PageSize, value: TEST_PAGE_SIZE));

                // Act
                AngleSharp.Dom.IElement searchBox = cut.Find(cssSelector: "input[aria-label='Søg i billeder']");
                await searchBox.InputAsync(eventArgs: new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "whale" });
                await Task.Delay(millisecondsDelay: SEARCH_DEBOUNCE_WAIT_MS, cancellationToken: Xunit.TestContext.Current.CancellationToken);

                // Assert
                cut.WaitForAssertion(assertion: () =>
                {
                        Assert.Contains(expected: "whale", collection: recordedSearches);
                        Assert.Contains(expectedSubstring: "whale.webp", actualString: cut.Markup);
                });
        }

        /// <summary>
        /// Verifies that configuration or API errors during catalog loading surface a danger toast instead of crashing the component.
        /// </summary>
        [Fact]
        public void InitialRender_WhenClientThrowsOptionsOrApiError_ShowsDangerToastGracefully()
        {
                // Arrange
                StubContentImageClient stubClient = new()
                {
                        OnListImagesQueryAsync = (_, _) => throw new OptionsValidationException(
                                optionsName: "ContentService",
                                optionsType: typeof(object),
                                failureMessages: ["BaseAddress missing"])
                };
                this.RegisterUiServices(imageClient: stubClient);
                ToastService toasts = this.Services.GetRequiredService<ToastService>();

                // Act
                IRenderedComponent<ContentImageGallery> cut = this.Render<ContentImageGallery>();

                // Assert
                IReadOnlyList<ToastMessage> activeToasts = toasts.GetToasts();
                Assert.Single(collection: activeToasts);
                Assert.Equal(expected: ToastVariant.Danger, actual: activeToasts[0].Variant);
                Assert.Contains(expectedSubstring: "Der er ikke uploadet nogen udstillingsbilleder endnu.", actualString: cut.Markup);
        }

        /// <summary>
        /// Verifies that clicking Next Page requests page 2 from <see cref="IContentServiceImageClient.ListImagesAsync(ContentImageQuery, CancellationToken)"/>.
        /// </summary>
        [Fact]
        public void Pagination_WhenNextClicked_LoadsSecondPage()
        {
                // Arrange
                List<int> requestedPages = [];
                StubContentImageClient stubClient = new()
                {
                        OnListImagesQueryAsync = (query, _) =>
                        {
                                requestedPages.Add(item: query.Page);
                                ContentImageResponse item = CreateMetadata(fileName: $"page-{query.Page}.png");
                                return Task.FromResult(result: new ContentPagedResponse<ContentImageResponse>
                                {
                                        Items = [item],
                                        Page = query.Page,
                                        PageSize = 1,
                                        TotalCount = 2
                                });
                        }
                };
                this.RegisterUiServices(imageClient: stubClient);

                IRenderedComponent<ContentImageGallery> cut = this.Render<ContentImageGallery>(
                        parameterBuilder: parameters => parameters.Add(parameterSelector: component => component.PageSize, value: 1));

                // Act
                AngleSharp.Dom.IElement nextButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Contains(value: "Næste", comparisonType: StringComparison.Ordinal));
                nextButton.Click();

                // Assert
                cut.WaitForAssertion(assertion: () =>
                {
                        Assert.Contains(expected: 2, collection: requestedPages);
                        Assert.Contains(expectedSubstring: "page-2.png", actualString: cut.Markup);
                });
        }

        private void RegisterUiServices(IContentServiceImageClient imageClient)
        {
                this.JSInterop.Mode = JSRuntimeMode.Loose;
                this.Services.AddPersonalUILibrary(configure: static _ => { });
                this.Services.AddSingleton<IContentServiceImageClient>(implementationInstance: imageClient);
        }

        private static ContentImageResponse CreateMetadata(
                string fileName,
                string? albumPath = null)
        {
                Guid id = Guid.NewGuid();
                return new ContentImageResponse
                {
                        Id = id,
                        PictureReference = $"picture.node-1.{id:D}",
                        ExternalId = $"node-1_{id:D}",
                        FileName = fileName,
                        ContentType = "image/png",
                        SizeBytes = 2048,
                        Sha256Hash = new string(c: 'd', count: 64),
                        AlbumPath = albumPath,
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };
        }
}
