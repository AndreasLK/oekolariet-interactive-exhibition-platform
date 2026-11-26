using ContentService.Facade.Dtos;

namespace ContentService.Api.Contracts;

/// <summary>
/// Separates the HTTP creation body from <see cref="CreateTemplateCommandDto"/> so the API can resolve trusted actor identity server-side without exposing internal command fields.
/// </summary>
/// <param name="Name">Unique human-readable template name used by curators to identify reusable layouts.</param>
/// <param name="ComponentType">Target Blazor UI component category so editors know which renderer owns the schema.</param>
/// <param name="SchemaJson">Raw JSON schema payload defining the template structure and image references.</param>
/// <param name="ActorId">Optional caller attribution used only when no authenticated principal identity is present.</param>
public sealed record class CreateTemplateRequest(
        string Name,
        TemplateComponentTypeDto ComponentType,
        string SchemaJson,
        string? ActorId = null);

