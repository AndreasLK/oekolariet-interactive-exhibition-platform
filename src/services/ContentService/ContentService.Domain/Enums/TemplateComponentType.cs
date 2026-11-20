namespace ContentService.Domain.Enums;

/// <summary>
/// Identifies the Blazor UI component category responsible for rendering a template in exhibition kiosks and the editor.
/// </summary>
// A closed enum with an explicit Custom escape hatch is used instead of a dynamic plugin registry so known presets remain strongly typed and database-safe via stable integer values.
public enum TemplateComponentType
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
        // Provides an escape hatch for custom installations without requiring enum additions or database schema changes.
        Custom = 3
}
