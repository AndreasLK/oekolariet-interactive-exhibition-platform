namespace ContentService.Domain.Enums;

/// <summary>
/// Represents the UI component type associated with a content template.
/// </summary>
public enum TemplateComponentType
{
        /// <summary>
        /// An interactive quiz template.
        /// </summary>
        QuizTemplate = 0,

        /// <summary>
        /// An interactive game template.
        /// </summary>
        GameTemplate = 1,

        /// <summary>
        /// A static or lightly interactive information screen template.
        /// </summary>
        InfoScreen = 2,

        /// <summary>
        /// A custom exhibition component template.
        /// </summary>
        Custom = 3
}
