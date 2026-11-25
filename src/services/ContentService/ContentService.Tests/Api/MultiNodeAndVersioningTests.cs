using System.Net;
using ContentService.Application.Common;
using ContentService.Application.Interfaces;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Application.UseCases.Images;
using ContentService.Application.UseCases.Publishing;
using ContentService.Application.UseCases.Templates;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Domain.Exceptions;
using ContentService.Tests.Api.Fakes;
using ContentService.Tests.Application;
using Microsoft.Extensions.Options;
using Xunit;

namespace ContentService.Tests.Api;

/// <summary>
/// Verifies API versioning, Dapr health probes, single-hop loop protection, and cross-node image resolution.
/// </summary>
public sealed class MultiNodeAndVersioningTests
{
        /// <summary>
        /// Verifies that <c>/healthz</c> is version-neutral, unversioned <c>/api/images</c> is rejected, and <c>/api/v1/images</c> emits <c>api-supported-versions</c>.
        /// </summary>
        [Fact]
        public async Task VersioningAndHealthz_EnforceExplicitV1RoutesAndVersionNeutralHealthProbeAsync()
        {
                InMemoryContentState state = new();
                await using FakeApiFactory factory = new(state: state);
                using HttpClient client = factory.CreateClient();

                // Verify the root /healthz probe succeeds without an API version prefix.
                using HttpResponseMessage healthResponse = await client.GetAsync(
                        requestUri: "/healthz",
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

                // Verify unversioned /api/images is rejected when explicit versioning is required.
                using HttpResponseMessage unversionedResponse = await client.GetAsync(
                        requestUri: "/api/images",
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.NotEqual(HttpStatusCode.OK, unversionedResponse.StatusCode);

                // Verify /api/v1/images succeeds and advertises supported versions in the response header.
                using HttpResponseMessage versionedResponse = await client.GetAsync(
                        requestUri: "/api/v1/images",
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, versionedResponse.StatusCode);
                Assert.True(versionedResponse.Headers.Contains("api-supported-versions"));
        }

        /// <summary>
        /// Verifies that a request already carrying <c>X-Node-Forwarded</c> for a remote node fails fast instead of looping.
        /// </summary>
        [Fact]
        public async Task ImageEndpoints_WhenAlreadyForwardedForRemoteNode_RejectsLoopAsync()
        {
                InMemoryContentState state = new();
                await using FakeApiFactory factory = new(state: state);
                using HttpClient client = factory.CreateClient();

                // Simulate a request that has already been forwarded once by setting X-Node-Forwarded: true.
                using HttpRequestMessage request = new(
                        method: HttpMethod.Get,
                        requestUri: $"/api/v1/images/node-2_{Guid.NewGuid():D}");
                request.Headers.Add(name: "X-Node-Forwarded", value: "true");

                // Expect 502 Bad Gateway instead of forwarding a second time.
                using HttpResponseMessage response = await client.SendAsync(
                        request: request,
                        cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }

        /// <summary>
        /// Verifies that <see cref="CreateTemplateUseCase"/> and <see cref="ResolvePublishBundleUseCase"/> batch-resolve remote node images via <see cref="IStorageNodeClient"/>.
        /// </summary>
        [Fact]
        public async Task CrossNodeTemplateAndBundle_ResolveRemoteImagesInSingleBatchPerNodeAsync()
        {
                FakeImageRepository imageRepo = new();
                FakeTemplateRepository templateRepo = new();
                Guid remoteImageId = Guid.NewGuid();
                string remoteNodeId = "node-2";
                FakeStorageNodeClient nodeClient = new();

                // Seed a remote image on node-2 so node-1 must resolve it over IStorageNodeClient.
                nodeClient.RemoteImages[(remoteNodeId, remoteImageId)] = new ImageMetadataResult
                {
                        Id = remoteImageId,
                        PictureReference = $"picture.{remoteNodeId}.{remoteImageId:D}",
                        ExternalId = $"{remoteNodeId}_{remoteImageId:D}",
                        FileName = "remote.png",
                        ContentType = "image/png",
                        SizeBytes = 128,
                        Sha256Hash = new string(c: 'a', count: 64),
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };

                IOptions<ContentStorageOptions> options = Options.Create(new ContentStorageOptions
                {
                        StorageNodeId = "node-1",
                        PeerStorageNodeIds = [remoteNodeId]
                });

                CreateTemplateUseCase createTemplate = new(
                        templateRepository: templateRepo,
                        imageRepository: imageRepo,
                        options: options,
                        storageNodeClient: nodeClient);

                // Create a template on node-1 referencing the remote image on node-2.
                TemplateResult template = await createTemplate.ExecuteAsync(
                        command: new CreateTemplateCommand
                        {
                                Name = "Cross-Node Template",
                                ComponentType = TemplateComponentType.InfoScreen,
                                SchemaJson = $"{{\"hero\":\"picture.{remoteNodeId}.{remoteImageId:D}\"}}",
                                ActorId = "editor-1"
                        },
                        cancellationToken: TestContext.Current.CancellationToken);

                // Confirm the remote reference was verified in a single batch call.
                Assert.Single(template.ReferencedPictureReferences);
                Assert.Equal(1, nodeClient.BatchCallCount);

                ResolvePublishBundleUseCase resolveBundle = new(
                        templateRepository: templateRepo,
                        imageRepository: imageRepo,
                        storageNodeClient: nodeClient,
                        options: options);

                // Resolve the publish bundle and verify the remote asset's ExternalId is preserved.
                PublishBundleResult bundle = await resolveBundle.ExecuteAsync(
                        command: new ResolvePublishBundleCommand
                        {
                                TemplateId = template.Id,
                                ImplementationJson = "{}"
                        },
                        cancellationToken: TestContext.Current.CancellationToken);

                Assert.Single(bundle.Assets);
                Assert.Equal($"{remoteNodeId}_{remoteImageId:D}", bundle.Assets[0].ExternalId);
        }

        /// <summary>
        /// Verifies that <see cref="DeleteImageUseCase"/> blocks deletion when a peer node template references the local image.
        /// </summary>
        [Fact]
        public async Task DeleteImageUseCase_WhenReferencedByPeerNodeTemplate_ThrowsEntityInUseExceptionAsync()
        {
                FakeImageRepository imageRepo = new();
                ImageEntity localImage = ImageEntity.Create(
                        storageNodeId: "node-1",
                        fileName: "local.png",
                        contentType: "image/png",
                        fileSizeBytes: 64,
                        sha256Hash: new string(c: 'b', count: 64),
                        actorId: "editor-1");
                await imageRepo.AddAsync(
                        metadata: localImage,
                        binary: ImageBinaryEntity.Create(imageId: localImage.Id, data: [1, 2, 3]),
                        cancellationToken: TestContext.Current.CancellationToken);

                // Mark the local image as referenced by a template on peer node-2.
                FakeStorageNodeClient nodeClient = new();
                nodeClient.ReferencedByPeerImageIds.Add(localImage.Id);

                IOptions<ContentStorageOptions> options = Options.Create(new ContentStorageOptions
                {
                        StorageNodeId = "node-1",
                        PeerStorageNodeIds = ["node-2"]
                });

                DeleteImageUseCase deleteUseCase = new(
                        imageRepository: imageRepo,
                        storageNodeClient: nodeClient,
                        options: options);

                // Expect EntityInUseException to protect cross-node template integrity.
                await Assert.ThrowsAsync<EntityInUseException>(() => deleteUseCase.ExecuteAsync(
                        command: new DeleteImageCommand
                        {
                                ImageId = localImage.Id,
                                ActorId = "editor-1"
                        },
                        cancellationToken: TestContext.Current.CancellationToken));
        }

        /// <summary>
        /// Verifies that a bare-GUID metadata lookup transparently falls back to peer storage nodes when absent locally.
        /// </summary>
        [Fact]
        public async Task GetImageMetadataUseCase_WhenBareGuidAbsentLocally_ResolvesFromPeerNodeAsync()
        {
                FakeImageRepository localRepo = new();
                Guid remoteImageId = Guid.NewGuid();
                FakeStorageNodeClient nodeClient = new();

                // Seed the image only on peer node-2 while localRepo remains empty.
                nodeClient.RemoteImages[("node-2", remoteImageId)] = new ImageMetadataResult
                {
                        Id = remoteImageId,
                        PictureReference = $"picture.node-2.{remoteImageId:D}",
                        ExternalId = $"node-2_{remoteImageId:D}",
                        FileName = "screenshot.png",
                        ContentType = "image/png",
                        SizeBytes = 256,
                        Sha256Hash = new string(c: 'c', count: 64),
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };

                IOptions<ContentStorageOptions> options = Options.Create(new ContentStorageOptions
                {
                        StorageNodeId = "node-1",
                        PeerStorageNodeIds = ["node-2"]
                });

                GetImageMetadataUseCase useCase = new(
                        imageRepository: localRepo,
                        storageNodeClient: nodeClient,
                        options: options);

                // Query using a bare GUID on node-1 and confirm it resolves from node-2.
                ImageMetadataResult resolved = await useCase.ExecuteAsync(
                        imageId: remoteImageId,
                        cancellationToken: TestContext.Current.CancellationToken);

                Assert.Equal(remoteImageId, resolved.Id);
                Assert.Equal($"node-2_{remoteImageId:D}", resolved.ExternalId);
        }

        /// <summary>
        /// Minimal in-memory <see cref="IStorageNodeClient"/> test double for verifying cross-node orchestration.
        /// </summary>
        private sealed class FakeStorageNodeClient : IStorageNodeClient
        {
                public Dictionary<(string NodeId, Guid ImageId), ImageMetadataResult> RemoteImages { get; } = [];

                public HashSet<Guid> ReferencedByPeerImageIds { get; } = [];

                public int BatchCallCount { get; private set; }

                public Task<ImageMetadataResult?> GetImageMetadataAsync(
                        string storageNodeId,
                        Guid imageId,
                        CancellationToken cancellationToken) =>
                        Task.FromResult(this.RemoteImages.GetValueOrDefault((storageNodeId, imageId)));

                public Task<IReadOnlyList<ImageMetadataResult>> GetImagesMetadataBatchAsync(
                        string storageNodeId,
                        IReadOnlyCollection<Guid> imageIds,
                        CancellationToken cancellationToken)
                {
                        this.BatchCallCount++;
                        List<ImageMetadataResult> results = [];
                        foreach (Guid imageId in imageIds)
                        {
                                if (this.RemoteImages.TryGetValue(key: (storageNodeId, imageId), value: out ImageMetadataResult? item))
                                {
                                        results.Add(item);
                                }
                        }

                        return Task.FromResult<IReadOnlyList<ImageMetadataResult>>(results);
                }

                public Task<ImageStreamResult?> OpenImageStreamAsync(
                        string storageNodeId,
                        Guid imageId,
                        bool isPreview,
                        CancellationToken cancellationToken) =>
                        Task.FromResult<ImageStreamResult?>(null);

                public Task<ImageMetadataResult> UpdateImageMetadataAsync(
                        string storageNodeId,
                        UpdateImageMetadataCommand command,
                        CancellationToken cancellationToken) =>
                        throw new NotSupportedException();

                public Task DeleteImageAsync(
                        string storageNodeId,
                        DeleteImageCommand command,
                        CancellationToken cancellationToken) =>
                        Task.CompletedTask;

                public Task<bool> IsImageReferencedByAnyTemplateAsync(
                        string peerStorageNodeId,
                        string imageStorageNodeId,
                        Guid imageId,
                        CancellationToken cancellationToken) =>
                        Task.FromResult(this.ReferencedByPeerImageIds.Contains(imageId));
        }

        /// <summary>
        /// Minimal in-memory <see cref="IImageRepository"/> test double.
        /// </summary>
        private sealed class FakeImageRepository : IImageRepository
        {
                private readonly Dictionary<Guid, ImageEntity> _images = [];

                public Task<ImageEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                        Task.FromResult(this._images.GetValueOrDefault(id));

                public Task<IReadOnlyList<ImageEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
                        Task.FromResult<IReadOnlyList<ImageEntity>>(
                                [.. ids.Where(id => this._images.ContainsKey(id)).Select(id => this._images[id])]);

                public Task<ImageEntity?> GetBySha256Async(string sha256Hash, CancellationToken cancellationToken) =>
                        Task.FromResult<ImageEntity?>(null);

                public Task<PagedResult<ImageEntity>> ListAsync(
                        int page, int pageSize, string? searchTerm, string? albumPath, bool includeSubalbums, CancellationToken cancellationToken) =>
                        Task.FromResult(new PagedResult<ImageEntity>
                        {
                                Items = [],
                                Page = page,
                                PageSize = pageSize,
                                TotalCount = 0
                        });

                public Task<IReadOnlyList<string>> ListChildAlbumPathsAsync(string? parentAlbumPath, CancellationToken cancellationToken) =>
                        Task.FromResult<IReadOnlyList<string>>([]);

                public Task<bool> IsReferencedByAnyTemplateAsync(Guid imageId, CancellationToken cancellationToken) =>
                        Task.FromResult(false);

                public Task AddAsync(ImageEntity metadata, ImageBinaryEntity binary, CancellationToken cancellationToken)
                {
                        this._images[metadata.Id] = metadata;
                        return Task.CompletedTask;
                }

                public Task DeleteAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                        Task.CompletedTask;

                public Task UpdateMetadataAsync(ImageEntity image, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                        Task.CompletedTask;
        }

        /// <summary>
        /// Minimal in-memory <see cref="ITemplateRepository"/> test double.
        /// </summary>
        private sealed class FakeTemplateRepository : ITemplateRepository
        {
                private readonly Dictionary<Guid, TemplateEntity> _templates = [];

                public Task<TemplateEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
                        Task.FromResult(this._templates.GetValueOrDefault(id));

                public Task<PagedResult<TemplateEntity>> ListAsync(
                        int page, int pageSize, TemplateComponentType? componentType, string? searchTerm, CancellationToken cancellationToken) =>
                        Task.FromResult(new PagedResult<TemplateEntity>
                        {
                                Items = [],
                                Page = page,
                                PageSize = pageSize,
                                TotalCount = 0
                        });

                public Task<bool> ExistsByNameAsync(string name, Guid? excludeTemplateId, CancellationToken cancellationToken) =>
                        Task.FromResult(false);

                public Task AddAsync(TemplateEntity template, CancellationToken cancellationToken)
                {
                        this._templates[template.Id] = template;
                        return Task.CompletedTask;
                }

                public Task UpdateAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                        Task.CompletedTask;

                public Task DeleteAsync(TemplateEntity template, Guid expectedConcurrencyStamp, CancellationToken cancellationToken) =>
                        Task.CompletedTask;
        }
}
