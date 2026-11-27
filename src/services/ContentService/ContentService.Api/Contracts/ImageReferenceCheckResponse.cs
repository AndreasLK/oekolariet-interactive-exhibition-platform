namespace ContentService.Api.Contracts;

/// <summary>
/// Response payload returned to peer storage nodes checking template references.
/// </summary>
/// <param name="IsReferenced">Indicates whether any active template on this node references the queried image.</param>
internal sealed record class ImageReferenceCheckResponse(bool IsReferenced);

