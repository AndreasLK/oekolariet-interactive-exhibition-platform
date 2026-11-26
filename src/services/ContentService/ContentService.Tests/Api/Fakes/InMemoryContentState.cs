using ContentService.Domain.Entities;

namespace ContentService.Tests.Api.Fakes;

/// <summary>
/// Shared mutable state backing in-memory API test doubles.
/// </summary>
internal sealed class InMemoryContentState
{
        public Dictionary<Guid, ImageEntity> Images { get; } = [];
        public Dictionary<Guid, byte[]> Binaries { get; } = [];
        public Dictionary<Guid, TemplateEntity> Templates { get; } = [];
        public HashSet<Guid> ReferencedImageIds { get; } = [];
        public long? ForcedUsedBytesOverride { get; set; }
        public Exception? ExceptionToThrowOnGetImage { get; set; }

        public void SeedImage(ImageEntity image, byte[]? binaryBytes)
        {
                this.Images[image.Id] = image;
                if (binaryBytes is not null)
                {
                        this.Binaries[image.Id] = binaryBytes;
                }
        }
}

