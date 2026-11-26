namespace ContentService.Sdk.Models;

/// <summary>
/// Command payload for requesting a healthy storage node upload offer across the ContentService pool.
/// </summary>
/// <param name="RequiredBytes">The minimum available storage quota in bytes required for the pending upload.</param>
public sealed record class ContentRequestUploadOfferCommand(long RequiredBytes);

