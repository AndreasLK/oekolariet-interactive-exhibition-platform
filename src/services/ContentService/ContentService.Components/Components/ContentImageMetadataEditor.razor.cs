using ALKsPersonalUI.Core;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;

namespace ContentService.Components.Components;

/// <summary>
/// Controlled metadata editing form for updating an image's searchable description (<c>AltText</c>), tags, and album path.
/// </summary>
/// <remarks>
/// Captures the <see cref="ContentImageResponse.ConcurrencyStamp"/> loaded into the form so ContentService can detect
/// optimistic concurrency conflicts (HTTP 409) if another editor updates or deletes the image first.
/// </remarks>
public sealed partial class ContentImageMetadataEditor : ComponentBase
{
        private const int MAX_ALT_TEXT_LENGTH = 500;
        private const int MAX_TAGS_LENGTH = 500;
        private const int MAX_ALBUM_PATH_LENGTH = 256;
        private const string TAG_DISPLAY_SEPARATOR = ", ";

        private Guid? _loadedImageId;
        private Guid? _loadedStamp;
        private string _altText = string.Empty;
        private string _tags = string.Empty;
        private string _albumPath = string.Empty;

        /// <summary>
        /// Gets or sets the image metadata record being edited.
        /// </summary>
        [Parameter]
        public ContentImageResponse? Image { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether metadata persistence is currently in flight.
        /// </summary>
        [Parameter]
        public bool IsSaving { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the user submits updated metadata.
        /// </summary>
        [Parameter]
        public EventCallback<ContentUpdateImageMetadataCommand> OnSaveRequested { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the user cancels metadata editing.
        /// </summary>
        [Parameter]
        public EventCallback OnCancelRequested { get; set; }

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
                if (this.Image is null)
                {
                        return;
                }

                // Refresh local form fields ONLY when switching to a different image or receiving a rotated ConcurrencyStamp from the server.
                // Without this guard, normal Blazor parent re-renders would overwrite what the user is currently typing.
                if (this._loadedImageId != this.Image.Id || this._loadedStamp != this.Image.ConcurrencyStamp)
                {
                        this._loadedImageId = this.Image.Id;
                        this._loadedStamp = this.Image.ConcurrencyStamp;
                        this._altText = this.Image.AltText ?? string.Empty;
                        this._tags = string.Join(separator: TAG_DISPLAY_SEPARATOR, values: this.Image.Tags);
                        this._albumPath = this.Image.AlbumPath ?? string.Empty;
                }
        }

        private void HandleAltTextChanged(string value)
        {
                this._altText = value;
        }

        private void HandleTagsChanged(string value)
        {
                this._tags = value;
        }

        private void HandleAlbumPathChanged(string value)
        {
                this._albumPath = value;
        }

        private Task HandleCancelClickedAsync() =>
                this.OnCancelRequested.InvokeAsync();

        /// <summary>
        /// Builds a trimmed <see cref="ContentUpdateImageMetadataCommand"/> stamped with the loaded <see cref="ContentImageResponse.ConcurrencyStamp"/>.
        /// </summary>
        private async Task HandleSaveClickedAsync()
        {
                if (this.Image is null || this.IsSaving)
                {
                        return;
                }

                ContentUpdateImageMetadataCommand request = new()
                {
                        // Convert blank fields to null so ContentService clears those columns instead of storing empty whitespace.
                        AltText = string.IsNullOrWhiteSpace(value: this._altText) ? null : this._altText.Trim(),
                        Tags = string.IsNullOrWhiteSpace(value: this._tags) ? null : this._tags.Trim(),
                        AlbumPath = string.IsNullOrWhiteSpace(value: this._albumPath) ? null : this._albumPath.Trim(),
                        // Send the exact stamp we loaded so SQL Server rejects stale overwrites with 409 Conflict.
                        ExpectedConcurrencyStamp = this.Image.ConcurrencyStamp
                };

                await this.OnSaveRequested.InvokeAsync(arg: request);
        }
}

