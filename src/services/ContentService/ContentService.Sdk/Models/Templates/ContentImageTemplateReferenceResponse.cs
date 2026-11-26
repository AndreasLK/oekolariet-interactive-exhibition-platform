namespace ContentService.Sdk.Models;

/// <summary>
/// Response payload indicating whether an exhibition image is referenced by any active template.
/// </summary>
/// <param name="IsReferenced"><see langword="true"/> when at least one active template references the queried image; otherwise, <see langword="false"/>.</param>
public sealed record class ContentImageTemplateReferenceResponse(bool IsReferenced);

