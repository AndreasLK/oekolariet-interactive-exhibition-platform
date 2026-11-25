namespace ContentService.Api.Contracts;

/// <summary>
/// Request payload for requesting a healthy upload offer across the storage node pool.
/// </summary>
/// <param name="RequiredBytes">The minimum available quota bytes required for the pending upload.</param>
internal sealed record class UploadOfferRequestBody(long RequiredBytes);

