namespace ContentService.Sdk.Models;

/// <summary>
/// Command payload for updating an existing exhibition template with optimistic concurrency protection.
/// </summary>
/// <param name="Name">Updated template display name.</param>
/// <param name="SchemaJson">Updated raw JSON schema payload.</param>
/// <param name="ExpectedConcurrencyStamp">Optimistic concurrency token preventing concurrent editors from silently overwriting newer saves.</param>
/// <param name="ActorId">Optional audit actor identifier overriding <c>ContentServiceClientOptions.DefaultActorId</c>.</param>
public sealed record class ContentUpdateTemplateCommand(
        string Name,
        string SchemaJson,
        Guid ExpectedConcurrencyStamp,
        string? ActorId = null);

