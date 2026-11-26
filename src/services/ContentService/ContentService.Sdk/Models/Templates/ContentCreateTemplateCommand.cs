namespace ContentService.Sdk.Models;

/// <summary>
/// Command payload for creating a new exhibition template in ContentService.
/// </summary>
/// <param name="Name">Unique human-readable template name used by curators to identify reusable layouts.</param>
/// <param name="ComponentType">Target Blazor UI component category so editors know which renderer owns the schema.</param>
/// <param name="SchemaJson">Raw JSON schema payload defining the template structure and image references.</param>
/// <param name="ActorId">Optional audit actor identifier overriding <c>ContentServiceClientOptions.DefaultActorId</c>.</param>
public sealed record class ContentCreateTemplateCommand(
        string Name,
        ContentTemplateComponentType ComponentType,
        string SchemaJson,
        string? ActorId = null);

