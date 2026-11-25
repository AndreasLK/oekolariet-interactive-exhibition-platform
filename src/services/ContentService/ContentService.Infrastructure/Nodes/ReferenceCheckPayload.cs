namespace ContentService.Infrastructure.Nodes;

/// <summary>
/// Internal DTO matching the peer template-reference check response.
/// </summary>
/// <param name="IsReferenced">Indicates whether any active template on the peer node references the image.</param>
internal sealed record class ReferenceCheckPayload(bool IsReferenced);

