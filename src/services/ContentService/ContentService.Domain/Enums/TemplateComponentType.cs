namespace ContentService.Domain.Enums;

// Maps each template to the Blazor UI component that renders it; explicit integer values keep persisted DB rows stable if members are reordered.
public enum TemplateComponentType
{
        QuizTemplate = 0,
        GameTemplate = 1,
        InfoScreen = 2,
        // Bespoke one-off exhibition components outside the standard quiz, game, and info-screen presets.
        Custom = 3
}
