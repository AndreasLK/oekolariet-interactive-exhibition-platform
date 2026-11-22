namespace ContentService.Tests.Sdk.TestDoubles;

/// <summary>
/// Immutable snapshot of one multipart form-data section captured by <see cref="StubHttpMessageHandler"/>.
/// </summary>
/// <param name="Name">The form field name.</param>
/// <param name="FileName">The ASCII fallback filename header parameter.</param>
/// <param name="FileNameStar">The UTF-8 <c>filename*</c> header parameter.</param>
/// <param name="ContentType">The MIME media type declared on the part.</param>
/// <param name="Bytes">The raw payload bytes written for the part.</param>
internal sealed record class CapturedMultipartPart(
        string? Name,
        string? FileName,
        string? FileNameStar,
        string? ContentType,
        byte[] Bytes);

