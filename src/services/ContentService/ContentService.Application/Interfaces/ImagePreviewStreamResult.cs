namespace ContentService.Application.Interfaces;

/// <summary>
/// Represents an open read-only stream for a stored image preview alongside its MIME content type and byte length.
/// </summary>
/// <param name="Content">The readable preview stream owned by the caller.</param>
/// <param name="ContentType">The MIME content type of the preview stream.</param>
/// <param name="SizeBytes">The byte length of the preview payload.</param>
public sealed record ImagePreviewStreamResult(
        Stream Content,
        string ContentType,
        long SizeBytes);

