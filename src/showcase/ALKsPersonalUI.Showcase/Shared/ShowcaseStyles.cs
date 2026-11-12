using ALKsPersonalUI.Core;
using ALKsPersonalUI.Overlays;
using Microsoft.JSInterop;

namespace ALKsPersonalUI.Showcase;

public static class ShowcaseStyles
{
    public static UIModifier SectionWindow => UIModifier.New
        .FillMaxWidth()
        .Padding(28)
        .Gap(20);

    public static UIModifier CardWindow => UIModifier.New
        .FillMaxWidth()
        .Padding(22)
        .Gap(14)
        .Background("var(--ui-bg-elevated)");

    public static UIModifier FullHeightCardWindow => UIModifier.New
        .FillMaxSize()
        .Padding(22)
        .Gap(16)
        .Background("var(--ui-bg-elevated)")
        .JustifyContent(Arrangement.SpaceBetween);

    public static UIModifier CodeWindow => UIModifier.New
        .FillMaxWidth()
        .Padding(18, 20)
        .Background("var(--ui-bg-header)")
        .BorderColor("var(--ui-border)")
        .HorizontalScroll();

    public static UIModifier ExpandingCodeWindow => UIModifier.New
        .FillMaxWidth()
        .Weight(1)
        .Padding(18, 20)
        .Background("var(--ui-bg-header)")
        .BorderColor("var(--ui-border)")
        .HorizontalScroll()
        .VerticalScroll();

    public static UIModifier PreText => UIModifier.New
        .FontSize("0.9rem")
        .Style("white-space", "pre")
        .Style("line-height", "1.55");

    public static UIModifier CopyButton => UIModifier.New
        .Padding(6, 14)
        .BaseColor("#93c5fd");

    public static async Task CopyAsync(IJSRuntime js, ToastService toast, string code, string label)
    {
        try
        {
            await js.InvokeVoidAsync("navigator.clipboard.writeText", code);
            toast.ShowSuccess($"Copied {label} to clipboard!");
        }
        catch
        {
            toast.ShowWarning("Clipboard access denied by browser.");
        }
    }
}