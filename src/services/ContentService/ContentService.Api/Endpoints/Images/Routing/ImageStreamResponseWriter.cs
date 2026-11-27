using ContentService.Api.Common;
using ContentService.Facade.Dtos;

namespace ContentService.Api.Endpoints.Images;

/// <summary>
/// Applies immutable caching/security headers and transfers image stream disposal ownership to ASP.NET Core.
/// </summary>
internal static class ImageStreamResponseWriter
{
        /// <summary>
        /// Configures caching/security response headers and hands stream disposal ownership to ASP.NET Core, cleaning up on header failure.
        /// </summary>
        /// <param name="streamDto">The open image stream DTO.</param>
        /// <param name="httpContext">The active HTTP context.</param>
        /// <returns>A streaming <see cref="IResult"/>.</returns>
        public static async Task<IResult> WriteAsync(
                ImageStreamDto streamDto,
                HttpContext httpContext)
        {
                // Dispose streamDto if header setup fails before Results.Stream assumes ownership.
                try
                {
                        ImageContentResponseHeaders.Configure(response: httpContext.Response, metadata: streamDto.Metadata);
                        return Results.Stream(
                                stream: streamDto.Content,
                                contentType: streamDto.Metadata.ContentType);
                }
                catch
                {
                        await streamDto.DisposeAsync();
                        throw;
                }
        }
}

