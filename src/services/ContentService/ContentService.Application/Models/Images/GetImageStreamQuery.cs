namespace ContentService.Application.Models;

/// <summary>
/// Query criteria for loading an exhibition image's readable byte stream.
/// </summary>
public sealed record class GetImageStreamQuery
{
        /// <summary>
        /// Gets the unique identifier of the requested image.
        /// </summary>
        public required Guid ImageId { get; init; }
}

