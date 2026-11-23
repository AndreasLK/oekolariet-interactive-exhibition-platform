using Bunit;
using ContentService.Components.Components;
using Xunit;

namespace ContentService.Tests.Components;

/// <summary>
/// Verifies cached <c>JsonUiNode</c> rendering and error handling in <see cref="ContentTemplatePreview"/>.
/// </summary>
public sealed class ContentTemplatePreviewTests : BunitContext
{
        /// <summary>
        /// Ensures valid <c>JsonUiNode</c> schema JSON renders live ALKsPersonalUI content.
        /// </summary>
        [Fact]
        public void ContentTemplatePreview_ValidSchemaJson_RendersJsonUiContent()
        {
                const string SCHEMA_JSON = """
                        {
                          "Type": "Column",
                          "Children": [
                            { "Type": "Text", "Props": { "Value": "Hello Exhibition" } }
                          ]
                        }
                        """;

                IRenderedComponent<ContentTemplatePreview> cut = this.Render<ContentTemplatePreview>(
                        parameters => parameters.Add(p => p.SchemaJson, SCHEMA_JSON));

                // Initial paint shows the lazy-loading placeholder, then mounts the JsonUiRenderer content afterwards.
                cut.WaitForAssertion(
                        assertion: () => Assert.Contains(expectedSubstring: "Hello Exhibition", actualString: cut.Markup, comparisonType: StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures invalid JSON displays a clear validation message instead of crashing the component tree.
        /// </summary>
        [Fact]
        public void ContentTemplatePreview_InvalidJson_DisplaysErrorMessage()
        {
                IRenderedComponent<ContentTemplatePreview> cut = this.Render<ContentTemplatePreview>(
                        parameters => parameters.Add(p => p.SchemaJson, "{ not valid json"));

                cut.WaitForAssertion(
                        assertion: () => Assert.Contains(expectedSubstring: "Invalid template layout JSON", actualString: cut.Markup, comparisonType: StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures the responsive container-query frame wraps the preview viewport and renders the empty message after lazy load when schema JSON is blank.
        /// </summary>
        [Fact]
        public void ContentTemplatePreview_BlankSchemaJson_RendersFrameViewportAndEmptyMessage()
        {
                const string CUSTOM_EMPTY_MESSAGE = "Nothing to preview";

                IRenderedComponent<ContentTemplatePreview> cut = this.Render<ContentTemplatePreview>(
                        parameters => parameters
                                .Add(p => p.SchemaJson, "   ")
                                .Add(p => p.EmptyMessage, CUSTOM_EMPTY_MESSAGE));

                cut.WaitForAssertion(assertion: () =>
                {
                        Assert.NotNull(@object: cut.Find(cssSelector: ".template-preview-frame > .template-preview-viewport > .template-preview-empty"));
                        Assert.Contains(expectedSubstring: CUSTOM_EMPTY_MESSAGE, actualString: cut.Markup, comparisonType: StringComparison.Ordinal);
                });
        }

        /// <summary>
        /// Ensures lazy loading initially renders the loading indicator before transitioning to the preview content.
        /// </summary>
        [Fact]
        public void ContentTemplatePreview_LazyLoadEnabled_RendersLoadingMessageBeforeContent()
        {
                const string CUSTOM_LOADING_MESSAGE = "Preparing preview...";
                const string SCHEMA_JSON = """
                        {
                          "Type": "Text",
                          "Props": { "Value": "Deferred Text" }
                        }
                        """;

                IRenderedComponent<ContentTemplatePreview> cut = this.Render<ContentTemplatePreview>(
                        parameters => parameters
                                .Add(p => p.SchemaJson, SCHEMA_JSON)
                                .Add(p => p.LoadingMessage, CUSTOM_LOADING_MESSAGE));

                Assert.Contains(expectedSubstring: CUSTOM_LOADING_MESSAGE, actualString: cut.Markup, comparisonType: StringComparison.Ordinal);
                cut.WaitForAssertion(
                        assertion: () => Assert.Contains(expectedSubstring: "Deferred Text", actualString: cut.Markup, comparisonType: StringComparison.Ordinal));
        }
}

