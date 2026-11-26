using ALKsPersonalUI.Core;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace ContentService.Web.Layout;

/// <summary>
/// Root layout hosting the <c>ALKsPersonalUI</c> scaffold, top navigation bar, and toast container.
/// </summary>
public sealed partial class MainLayout : LayoutComponentBase, IDisposable
{
        private const double SCAFFOLD_PADDING_PX = 24;
        private const double CONTENT_MAX_WIDTH_PX = 1280;
        private const string DEV_PREVIEW_ROUTE = "/";
        private const string USER_PREVIEW_ROUTE = "/user-preview";

        private bool _isDisposed;

        [Inject]
        private NavigationManager Navigation { get; set; } = default!;

        /// <inheritdoc />
        protected override void OnInitialized()
        {
                this.Navigation.LocationChanged += this.HandleLocationChanged;
        }

        /// <inheritdoc />
        public void Dispose()
        {
                if (this._isDisposed)
                {
                        return;
                }

                this._isDisposed = true;
                this.Navigation.LocationChanged -= this.HandleLocationChanged;
        }

        private bool IsUserPreviewActive()
        {
                string relativePath = this.Navigation.ToBaseRelativePath(uri: this.Navigation.Uri);
                return relativePath.StartsWith(value: "user-preview", comparisonType: StringComparison.OrdinalIgnoreCase);
        }

        private void NavigateToDevPreview() => this.Navigation.NavigateTo(uri: DEV_PREVIEW_ROUTE);

        private void NavigateToUserPreview() => this.Navigation.NavigateTo(uri: USER_PREVIEW_ROUTE);

        private void HandleLocationChanged(object? sender, LocationChangedEventArgs e) => this.StateHasChanged();

        private UIModifier BuildScaffoldModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .AlignItems(alignment: HorizontalAlignment.Center);

        private UIModifier BuildContentColumnModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .MaxWidth(px: CONTENT_MAX_WIDTH_PX)
                        .Padding(uniformPx: SCAFFOLD_PADDING_PX);
}
