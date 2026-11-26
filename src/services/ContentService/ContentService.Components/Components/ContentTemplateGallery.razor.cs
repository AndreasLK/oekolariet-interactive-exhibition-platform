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
        private const int FIRST_PAGE = 1;
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

        private readonly List<ContentTemplateMetadata> _templates = [];
        private int _totalCount;
        private int _currentPage = FIRST_PAGE;
        private int _totalPages;
        private string? _searchTerm;
        private ContentTemplateComponentType? _selectedType;
        private ContentTemplateComponentType? _appliedDefaultComponentType;
        private Guid? _selectedTemplateId;
        private bool _hasInitialized;
        private bool _isLoading;
        private bool _isEditorOpen;
        private bool _isPreviewTabActive;
        private bool _isSaving;
        private ContentTemplateMetadata? _editingTemplate;
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
        public EventCallback<ContentTemplateMetadata> OnTemplateSelected { get; set; }

        /// <summary>
        /// Gets or sets the controlled selected template identifier used by picker modals with explicit confirmation.
        /// </summary>
        [Parameter]
        public Guid? SelectedTemplateId { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when pending selection changes (including deselection) without immediately confirming.
        /// </summary>
        [Parameter]
        public EventCallback<ContentTemplateMetadata?> OnSelectionChanged { get; set; }

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
                if (this.OnSelectionChanged.HasDelegate)
                {
                        this._selectedTemplateId = this.SelectedTemplateId;
                }

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
                // Reset to page 1 when the host changes the default component type filter.
                this._currentPage = FIRST_PAGE;
                return this.LoadTemplatesAsync();
        }

        private async Task LoadTemplatesAsync()
        {
                this._isLoading = true;
                try
                {
                        int clampedPageSize = Math.Max(val1: MIN_PAGE_SIZE, val2: this.PageSize);
                        ContentPagedResult<ContentTemplateMetadata> page = await this.TemplateClient.ListTemplatesAsync(
                                query: new ContentTemplateQuery
                                {
                                        Page = this._currentPage,
                                        PageSize = clampedPageSize,
                                        ComponentType = this._selectedType,
                                        SearchTerm = this._searchTerm
                                });

                        this._templates.Clear();
                        this._templates.AddRange(collection: page.Items);
                        this._totalCount = page.TotalCount;
                        this._totalPages = page.TotalPages;
                        // Clamp the active page if archiving or filtering reduces the total page count below it.
                        this._currentPage = Math.Min(val1: this._currentPage, val2: Math.Max(val1: FIRST_PAGE, val2: this._totalPages));
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
                // Reset to page 1 so a narrowed search never stays on an out-of-range page.
                this._currentPage = FIRST_PAGE;
                return this.LoadTemplatesAsync();
        }

        private Task HandleFilterTypeAsync(ContentTemplateComponentType? componentType)
        {
                this._selectedType = componentType;
                // Reset to page 1 so switching component types starts from the beginning of the filtered list.
                this._currentPage = FIRST_PAGE;
                return this.LoadTemplatesAsync();
        }

        private Task HandleRefreshClickedAsync() => this.LoadTemplatesAsync();

        private Task HandlePreviousPageClickedAsync()
        {
                if (this._isLoading || this._currentPage <= FIRST_PAGE)
                {
                        return Task.CompletedTask;
                }

                this._currentPage--;
                return this.LoadTemplatesAsync();
        }

        private Task HandleNextPageClickedAsync()
        {
                if (this._isLoading || this._currentPage >= this._totalPages)
                {
                        return Task.CompletedTask;
                }

                this._currentPage++;
                return this.LoadTemplatesAsync();
        }

        private void HandleOpenCreateModal()
        {
                this._editingTemplate = null;
                this._draftName = string.Empty;
                this._draftComponentType = ContentTemplateComponentType.InfoScreen;
                this._draftSchemaJson = DEFAULT_TEMPLATE_JSON;
                this._isPreviewTabActive = false;
                this._isEditorOpen = true;
        }

        private void HandleOpenEditModal(ContentTemplateMetadata template)
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
                                await this.TemplateClient.CreateTemplateAsync(request: new CreateContentTemplateRequest
                                {
                                        Name = this._draftName.Trim(),
                                        ComponentType = this._draftComponentType,
                                        SchemaJson = this._draftSchemaJson
                                });
                                this.Toasts.ShowSuccess(messageText: $"Created template '{this._draftName.Trim()}'.");
                        }
                        else
                        {
                                await this.TemplateClient.UpdateTemplateAsync(
                                        templateId: this._editingTemplate.Id,
                                        request: new UpdateContentTemplateRequest
                                        {
                                                Name = this._draftName.Trim(),
                                                SchemaJson = this._draftSchemaJson,
                                                ExpectedConcurrencyStamp = this._editingTemplate.ConcurrencyStamp
                                        });
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

        private async Task HandleArchiveTemplateAsync(ContentTemplateMetadata template)
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

        private const double CARD_PADDING_PX = 16;
        private const double CARD_BORDER_RADIUS_PX = 12;
        private const double SELECTED_CARD_BORDER_PX = 2;
        private const double DEFAULT_CARD_BORDER_PX = 1;

        private bool IsSelectionEnabled() =>
                this.OnTemplateSelected.HasDelegate || this.OnSelectionChanged.HasDelegate;

        private async Task HandleTemplateSelectionToggledAsync(ContentTemplateMetadata template)
        {
                if (!this.IsSelectionEnabled())
                {
                        return;
                }

                // Toggle selection state cleanly and notify the host of either the selected template or deselection.
                bool isSelecting = this._selectedTemplateId != template.Id;
                this._selectedTemplateId = isSelecting ? template.Id : null;

                if (this.OnSelectionChanged.HasDelegate)
                {
                        await this.OnSelectionChanged.InvokeAsync(arg: isSelecting ? template : null);
                }

                if (isSelecting && this.OnTemplateSelected.HasDelegate)
                {
                        await this.OnTemplateSelected.InvokeAsync(arg: template);
                }
        }

        private UIModifier BuildTemplateCardModifier(bool isSelected)
        {
                UIModifier modifier = UIModifier.New
                        .FillMaxWidth()
                        .Padding(uniformPx: CARD_PADDING_PX)
                        .BorderRadius(px: CARD_BORDER_RADIUS_PX);

                return isSelected
                        ? modifier.Border(thicknessPx: SELECTED_CARD_BORDER_PX, borderColor: "var(--ui-accent, #7c3aed)")
                        : modifier.Border(thicknessPx: DEFAULT_CARD_BORDER_PX, borderColor: "var(--ui-border, rgba(255, 255, 255, 0.14))");
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
