using ALKsPersonalUI.Core;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;

namespace ContentService.Components.Components;

/// <summary>
/// Ready-to-use modal window wrapping <see cref="ContentTemplateGallery"/> for selecting a layout template from ContentService.
/// </summary>
public sealed partial class ContentTemplatePickerModal : ComponentBase
{
        private const string DEFAULT_TITLE = "Vælg skabelon";
        private const int DEFAULT_PAGE_SIZE = 12;
        private const string BODY_MAX_HEIGHT = "70vh";

        private ContentTemplateResponse? _pendingSelectedTemplate;
        private bool _wasOpen;

        /// <summary>
        /// Gets or sets a value indicating whether the template picker modal is open.
        /// </summary>
        [Parameter]
        public bool IsOpen { get; set; }

        /// <summary>
        /// Gets or sets the two-way binding callback for <see cref="IsOpen"/>.
        /// </summary>
        [Parameter]
        public EventCallback<bool> IsOpenChanged { get; set; }

        /// <summary>
        /// Gets or sets the modal header title.
        /// </summary>
        [Parameter]
        public string Title { get; set; } = DEFAULT_TITLE;

        /// <summary>
        /// Gets or sets the callback invoked when the user selects a template.
        /// </summary>
        [Parameter]
        public EventCallback<ContentTemplateResponse> OnTemplateSelected { get; set; }

        /// <summary>
        /// Gets or sets the optional default component type filter applied when the picker opens.
        /// </summary>
        [Parameter]
        public ContentTemplateComponentType? DefaultComponentType { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether create, edit, and archive actions are enabled inside the picker.
        /// </summary>
        [Parameter]
        public bool AllowManage { get; set; }

        /// <summary>
        /// Gets or sets the number of templates requested per page.
        /// </summary>
        [Parameter]
        public int PageSize { get; set; } = DEFAULT_PAGE_SIZE;

        /// <summary>
        /// Gets or sets a value indicating whether the modal closes automatically after a template is selected.
        /// </summary>
        [Parameter]
        public bool CloseOnSelect { get; set; } = true;

        /// <inheritdoc />
        protected override void OnParametersSet()
        {
                // Clear any stale pending selection whenever the modal closes or freshly opens.
                if (!this.IsOpen)
                {
                        this._wasOpen = false;
                        this._pendingSelectedTemplate = null;
                        return;
                }

                if (!this._wasOpen)
                {
                        this._wasOpen = true;
                        this._pendingSelectedTemplate = null;
                }
        }

        private void HandlePendingSelectionChanged(ContentTemplateResponse? template) =>
                this._pendingSelectedTemplate = template;

        private async Task HandleConfirmSelectionAsync()
        {
                if (this._pendingSelectedTemplate is null)
                {
                        return;
                }

                await this.OnTemplateSelected.InvokeAsync(arg: this._pendingSelectedTemplate);
                if (this.CloseOnSelect)
                {
                        await this.IsOpenChanged.InvokeAsync(arg: false);
                }
        }

        private Task HandleCloseAsync() => this.IsOpenChanged.InvokeAsync(arg: false);

        private UIModifier BuildModalModifier() =>
                UIModifier.New
                        .FillMaxWidth();

        private UIModifier BuildBodyModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .MaxHeight(BODY_MAX_HEIGHT)
                        .VerticalScroll();
}

