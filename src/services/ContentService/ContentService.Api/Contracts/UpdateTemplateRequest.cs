using ContentService.Facade.Dtos;

namespace ContentService.Api.Contracts;

/// <summary>
/// Separates the HTTP update body from <see cref="UpdateTemplateCommandDto"/> so the route <c>{id}</c> remains authoritative and callers must prove they hold the latest version.
/// </summary>
/// <param name="Name">Updated template name.</param>
/// <param name="SchemaJson">Updated raw JSON schema payload.</param>
/// <param name="ExpectedConcurrencyStamp">Optimistic concurrency token preventing concurrent editors from silently overwriting newer saves.</param>
/// <param name="ActorId">Optional caller attribution used only when no authenticated principal identity is present.</param>
public sealed record class UpdateTemplateRequest(
        string Name,
        string SchemaJson,
        Guid ExpectedConcurrencyStamp,
        string? ActorId = null);

