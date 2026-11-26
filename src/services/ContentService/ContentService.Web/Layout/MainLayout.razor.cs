using ALKsPersonalUI.Core;
using Microsoft.AspNetCore.Components;

namespace ContentService.Web.Layout;

/// <summary>
/// Root layout hosting the <c>ALKsPersonalUI</c> scaffold, top navigation bar, and toast container.
/// </summary>
public sealed partial class MainLayout : LayoutComponentBase
{
        private const double SCAFFOLD_PADDING_PX = 24;
        private const double CONTENT_MAX_WIDTH_PX = 1280;

        private UIModifier BuildScaffoldModifier() =>
                UIModifier.New
                        .AlignItems(alignment: HorizontalAlignment.Center)
                        .Padding(uniformPx: SCAFFOLD_PADDING_PX);

        private UIModifier BuildContentColumnModifier() =>
                UIModifier.New
                        .FillMaxWidth()
                        .MaxWidth(px: CONTENT_MAX_WIDTH_PX);
}
