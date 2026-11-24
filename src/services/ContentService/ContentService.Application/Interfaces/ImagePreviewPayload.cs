namespace ContentService.Application.Interfaces;

/// <summary>
/// Represents an encoded lightweight image preview payload and its MIME content type.
/// </summary>
/// <param name="Bytes">The encoded preview image bytes.</param>
/// <param name="ContentType">The MIME content type of the encoded preview (for example, <c>image/webp</c>).</param>
public sealed record ImagePreviewPayload(byte[] Bytes, string ContentType);

