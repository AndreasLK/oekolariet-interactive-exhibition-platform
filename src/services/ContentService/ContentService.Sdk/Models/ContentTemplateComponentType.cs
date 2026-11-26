namespace ContentService.Sdk.Models;

/// <summary>
/// Identifies the Blazor UI component category responsible for rendering a template.
/// </summary>
public enum ContentTemplateComponentType
{
        /// <summary>
        /// Renders an interactive quiz experience.
        /// </summary>
        QuizTemplate = 0,

        /// <summary>
        /// Renders an interactive game experience.
        /// </summary>
        GameTemplate = 1,

        /// <summary>
        /// Renders a static or animated informational exhibition screen.
        /// </summary>
        InfoScreen = 2,

        /// <summary>
        /// Renders a bespoke one-off exhibition component outside the standard presets.
        /// </summary>
        Custom = 3
}

