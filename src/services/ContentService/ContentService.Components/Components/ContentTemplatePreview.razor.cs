using System.Text.Json;
using ALKsPersonalUI.Json;
using Microsoft.AspNetCore.Components;

namespace ContentService.Components.Components;

/// <summary>
/// Renders a contained, non-interactive scaled preview ("screenshot") of an exhibition template's <see cref="JsonUiNode"/> layout schema, caching the parsed node tree until the JSON changes.
/// </summary>
public sealed partial class ContentTemplatePreview : ComponentBase, IDisposable
{
        private const int DEFER_RENDER_DELAY_MILLISECONDS = 1;
        private const string DEFAULT_EMPTY_MESSAGE = "No layout schema provided.";
        private const string DEFAULT_LOADING_MESSAGE = "Loading preview...";

        private readonly CancellationTokenSource _disposeCts = new();
        private string? _lastParsedJson;
        private JsonUiNode? _cachedNode;
        private string? _parseError;
        private bool _isPreviewLoaded;
        private bool _isDisposed;

        /// <summary>
        /// Gets or sets the raw JSON layout schema (<c>JsonUiNode</c> tree) to preview.
        /// </summary>
        [Parameter]
        public string? SchemaJson { get; set; }

        /// <summary>
        /// Gets or sets the message displayed when <see cref="SchemaJson"/> is empty or whitespace.
        /// </summary>
        [Parameter]
        public string EmptyMessage { get; set; } = DEFAULT_EMPTY_MESSAGE;

        /// <summary>
        /// Gets or sets a value indicating whether schema parsing and <c>JsonUiRenderer</c> mounting are deferred until after the initial page paint.
        /// </summary>
        [Parameter]
        public bool IsLazyLoadEnabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the loading text displayed while the preview is deferred.
        /// </summary>
        [Parameter]
        public string LoadingMessage { get; set; } = DEFAULT_LOADING_MESSAGE;

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
                // When lazy loading is disabled or the initial paint has already completed, parse immediately.
                if (!this.IsLazyLoadEnabled || this._isPreviewLoaded)
                {
                        this._isPreviewLoaded = true;
                        this.EnsureSchemaParsed();
                }
        }

        /// <inheritdoc />
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
                if (!firstRender || this._isPreviewLoaded || this._isDisposed)
                {
                        return;
                }

                // Yield past the browser's first paint so the surrounding page and gallery cards appear immediately.
                try
                {
                        await Task.Delay(millisecondsDelay: DEFER_RENDER_DELAY_MILLISECONDS, cancellationToken: this._disposeCts.Token);
                }
                catch (OperationCanceledException)
                {
                        return;
                }

                if (this._isDisposed)
                {
                        return;
                }

                this._isPreviewLoaded = true;
                this.EnsureSchemaParsed();
                this.StateHasChanged();
        }

        /// <inheritdoc />
        public void Dispose()
        {
                if (this._isDisposed)
                {
                        return;
                }

                this._isDisposed = true;
                this._disposeCts.Cancel();
                this._disposeCts.Dispose();
        }

        private void EnsureSchemaParsed()
        {
                // Skip JSON deserialization on unrelated Blazor re-renders when the layout string has not changed.
                if (string.Equals(a: this._lastParsedJson, b: this.SchemaJson, comparisonType: StringComparison.Ordinal))
                {
                        return;
                }

                this._lastParsedJson = this.SchemaJson;
                this._parseError = null;

                if (string.IsNullOrWhiteSpace(value: this.SchemaJson))
                {
                        this._cachedNode = null;
                        return;
                }

                try
                {
                        this._cachedNode = JsonUiNode.FromJson(json: this.SchemaJson);
                }
                catch (JsonException ex)
                {
                        this._cachedNode = null;
                        this._parseError = ex.Message;
                }
        }
}
