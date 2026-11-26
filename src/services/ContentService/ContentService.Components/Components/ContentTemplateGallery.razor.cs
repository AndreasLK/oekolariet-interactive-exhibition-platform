using ALKsPersonalUI.Core;
using ALKsPersonalUI.Overlays;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Exceptions;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;

namespace ContentService.Components.Components;

/// <summary>
/// Drop-in Blazor component for browsing, filtering, creating, editing, and live-previewing ContentService layout templates.
/// </summary>
public sealed partial class ContentTemplateGallery : ComponentBase
{
        private const int DEFAULT_PAGE = 1;
        private const int MIN_PAGE_SIZE = 1;
        private const int DEFAULT_PAGE_SIZE = 12;
        private const double EDITOR_MODAL_MAX_WIDTH_PX = 960;
        private const string EDITOR_BODY_MAX_HEIGHT = "65vh";
        private const int SCHEMA_TEXTAREA_ROWS = 12;
        private const int SCHEMA_TEXTAREA_MAX_LENGTH = 65536;
        private const string TEMPLATE_CARD_MIN_WIDTH = "320px";
        private const string DEFAULT_TEMPLATE_JSON = """
                {
                  "Type": "Column",
                  "Props": { "Gap": "0.5rem" },
                  "Children": [
                    { "Type": "Text", "Props": { "Value": "Exhibition Screen Title", "Variant": "H2" } },
                    { "Type": "Text", "Props": { "Value": "Edit the JsonUiNode schema to preview layout changes.", "Muted": "true" } }
                  ]
                }
                """;

        private readonly List<ContentTemplateResponse> _templates = [];
        private int _totalCount;
        private string? _searchTerm;
        private ContentTemplateComponentType? _selectedType;
        private ContentTemplateComponentType? _appliedDefaultComponentType;
        private Guid? _selectedTemplateId;
        private bool _hasInitialized;
        private bool _isLoading;
        private bool _isEditorOpen;
        private bool _isPreviewTabActive;
        private bool _isSaving;
        private ContentTemplateResponse? _editingTemplate;
        private string _draftName = string.Empty;
        private ContentTemplateComponentType _draftComponentType = ContentTemplateComponentType.InfoScreen;
        private string _draftSchemaJson = DEFAULT_TEMPLATE_JSON;

        [Inject]
        private IContentServiceTemplateClient TemplateClient { get; set; } = default!;

        [Inject]
        private ToastService Toasts { get; set; } = default!;

        /// <summary>
        /// Gets or sets the callback invoked when a user selects a template preview card.
        /// </summary>
        /// <remarks>
        /// Setting this parameter enables selection mode on template cards, matching <see cref="ContentImageGallery.OnImageSelected"/>.
        /// </remarks>
        [Parameter]
        public EventCallback<ContentTemplateResponse> OnTemplateSelected { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether create, edit, and archive actions are enabled.
        /// </summary>
        [Parameter]
        public bool AllowManage { get; set; } = true;

        /// <summary>
        /// Gets or sets the optional default component type filter applied when the gallery initializes or changes context.
        /// </summary>
        [Parameter]
        public ContentTemplateComponentType? DefaultComponentType { get; set; }

        /// <summary>
        /// Gets or sets the number of templates requested per gallery page (minimum 1).
        /// </summary>
        [Parameter]
        public int PageSize { get; set; } = DEFAULT_PAGE_SIZE;

        /// <inheritdoc />
        protected override Task OnInitializedAsync()
        {
                // Track initialization so OnParametersSetAsync does not trigger a duplicate load on first render.
                this._hasInitialized = true;
                this._appliedDefaultComponentType = this.DefaultComponentType;
                this._selectedType = this.DefaultComponentType;
                return this.LoadTemplatesAsync();
        }

        /// <inheritdoc />
        protected override Task OnParametersSetAsync()
        {
                if (!this._hasInitialized)
                {
                        return Task.CompletedTask;
                }

                // Reload only when the host explicitly changes the default component type filter.
                if (this._appliedDefaultComponentType == this.DefaultComponentType)
                {
                        return Task.CompletedTask;
                }

                this._appliedDefaultComponentType = this.DefaultComponentType;
                this._selectedType = this.DefaultComponentType;
                return this.LoadTemplatesAsync();
        }

        private async Task LoadTemplatesAsync()
        {
                this._isLoading = true;
                try
                {
                        int clampedPageSize = Math.Max(val1: MIN_PAGE_SIZE, val2: this.PageSize);
                        ContentPagedResponse<ContentTemplateResponse> page = await this.TemplateClient.ListTemplatesAsync(
                                query: new ContentTemplateQuery
                                {
                                        Page = DEFAULT_PAGE,
                                        PageSize = clampedPageSize,
                                        ComponentType = this._selectedType,
                                        SearchTerm = this._searchTerm
                                });

                        this._templates.Clear();
                        this._templates.AddRange(collection: page.Items);
                        this._totalCount = page.TotalCount;
                }
                catch (Exception ex) when (ex is ContentServiceApiException or HttpRequestException or InvalidOperationException)
                {
                        this.Toasts.ShowDanger(messageText: $"Failed to load templates: {ex.Message}");
                }
                finally
                {
                        this._isLoading = false;
                }
        }

        private Task HandleSearchChangedAsync(string? value)
        {
                this._searchTerm = value;
                return this.LoadTemplatesAsync();
        }

        private Task HandleFilterTypeAsync(ContentTemplateComponentType? componentType)
        {
                this._selectedType = componentType;
                return this.LoadTemplatesAsync();
        }

        private Task HandleRefreshClickedAsync() => this.LoadTemplatesAsync();

        private void HandleOpenCreateModal()
        {
                this._editingTemplate = null;
                this._draftName = string.Empty;
                this._draftComponentType = ContentTemplateComponentType.InfoScreen;
                this._draftSchemaJson = DEFAULT_TEMPLATE_JSON;
                this._isPreviewTabActive = false;
                this._isEditorOpen = true;
        }

        private void HandleOpenEditModal(ContentTemplateResponse template)
        {
                this._editingTemplate = template;
                this._draftName = template.Name;
                this._draftComponentType = template.ComponentType;
                this._draftSchemaJson = template.SchemaJson;
                this._isPreviewTabActive = false;
                this._isEditorOpen = true;
        }

        private void HandleEditorVisibilityChanged(bool isOpen) => this._isEditorOpen = isOpen;

        private void HandleCloseEditor() => this._isEditorOpen = false;

        private void HandleDraftNameChanged(string? value) => this._draftName = value ?? string.Empty;

        private void HandleDraftSchemaChanged(string? value) => this._draftSchemaJson = value ?? string.Empty;

        private async Task HandleSaveTemplateAsync()
        {
                if (string.IsNullOrWhiteSpace(value: this._draftName) || string.IsNullOrWhiteSpace(value: this._draftSchemaJson))
                {
                        this.Toasts.ShowDanger(messageText: "Template name and layout JSON are required.");
                        return;
                }

                this._isSaving = true;
                try
                {
                        if (this._editingTemplate is null)
                        {
                                await this.TemplateClient.CreateTemplateAsync(command: new ContentCreateTemplateCommand(
                                        Name: this._draftName.Trim(),
                                        ComponentType: this._draftComponentType,
                                        SchemaJson: this._draftSchemaJson));
                                this.Toasts.ShowSuccess(messageText: $"Created template '{this._draftName.Trim()}'.");
                        }
                        else
                        {
                                // Pass ExpectedConcurrencyStamp so concurrent edits by another curator fail with 409 Conflict instead of silently overwriting.
                                await this.TemplateClient.UpdateTemplateAsync(
                                        templateId: this._editingTemplate.Id,
                                        command: new ContentUpdateTemplateCommand(
                                                Name: this._draftName.Trim(),
                                                SchemaJson: this._draftSchemaJson,
                                                ExpectedConcurrencyStamp: this._editingTemplate.ConcurrencyStamp));
                                this.Toasts.ShowSuccess(messageText: $"Updated template '{this._draftName.Trim()}'.");
                        }

                        this._isEditorOpen = false;
                        await this.LoadTemplatesAsync();
                }
                catch (Exception ex) when (ex is ContentServiceApiException or HttpRequestException or InvalidOperationException)
                {
                        this.Toasts.ShowDanger(messageText: $"Failed to save template: {ex.Message}");
                }
                finally
                {
                        this._isSaving = false;
                }
        }

        private async Task HandleArchiveTemplateAsync(ContentTemplateResponse template)
        {
                try
                {
                        await this.TemplateClient.DeleteTemplateAsync(templateId: template.Id);
                        this.Toasts.ShowSuccess(messageText: $"Archived template '{template.Name}'.");
                        await this.LoadTemplatesAsync();
                }
                catch (Exception ex) when (ex is ContentServiceApiException or HttpRequestException or InvalidOperationException)
                {
                        this.Toasts.ShowDanger(messageText: $"Failed to archive template: {ex.Message}");
                }
        }

        private async Task HandleTemplateSelectionToggledAsync(ContentTemplateResponse template)
        {
                if (!this.OnTemplateSelected.HasDelegate)
                {
                        return;
                }

                // Toggle selection state cleanly and notify the host only when an item is actively selected.
                bool isSelecting = this._selectedTemplateId != template.Id;
                this._selectedTemplateId = isSelecting ? template.Id : null;
                if (isSelecting)
                {
                        await this.OnTemplateSelected.InvokeAsync(arg: template);
                }
        }

        private UIModifier BuildEditorModalModifier() =>
                UIModifier.New
                        .MaxWidth(px: EDITOR_MODAL_MAX_WIDTH_PX)
                        .FillMaxWidth();

        private UIModifier BuildEditorBodyModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .MaxHeight(EDITOR_BODY_MAX_HEIGHT)
                        .VerticalScroll();
}
