using ALKsPersonalUI;
using ALKsPersonalUI.Core;
using ContentService.Sdk;
using ContentService.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

const string APP_ROOT_SELECTOR = "#app";
const string HEAD_OUTLET_SELECTOR = "head::after";
const string THEME_PRESET_NAME = "OekolarietContent";
const string THEME_ACCENT_COLOR = "#2563eb";
const string THEME_BACKGROUND_COLOR = "#0f172a";
const string THEME_ACTION_COLOR = "#3b82f6";

WebAssemblyHostBuilder builder = WebAssemblyHostBuilder.CreateDefault(args: args);
builder.RootComponents.Add<App>(selector: APP_ROOT_SELECTOR);
builder.RootComponents.Add<HeadOutlet>(selector: HEAD_OUTLET_SELECTOR);

// Reference Blazor WebAssembly host setup for any app embedding ContentService.Components (e.g., EditorService):
// 1. Register ALKsPersonalUI (theme, ToastService, UI primitives required by ContentImageGallery).
// 2. Register ContentService.Sdk (provides IContentServiceImageClient + ContentServiceClientOptions validation).
// Register ALKsPersonalUI with automatic mouse/touch press physics and an exhibition-friendly dark seed palette.
builder.Services.AddPersonalUILibrary(configure: static options =>
{
        options.DefaultPressEffect = ButtonPressEffect.Auto;
        options.DefaultTouchInputMode = TouchInputMode.Auto;
        options.AddSeedTheme(
                name: THEME_PRESET_NAME,
                accentColor: THEME_ACCENT_COLOR,
                applicationBackgroundColor: THEME_BACKGROUND_COLOR,
                actionColor: THEME_ACTION_COLOR,
                isDarkTheme: true,
                defaultPressEffect: ButtonPressEffect.Auto);
        options.InitialPresetName = THEME_PRESET_NAME;
});

// Bind ContentService SDK BaseAddress and actor defaults from wwwroot/appsettings.json ("ContentService" section).
// Note: ContentService.Api must allow this WASM origin in CORS when hosted on a different port/domain.
builder.Services.AddContentServiceSdk(configuration: builder.Configuration);

await builder.Build().RunAsync();
