using System.Security.Cryptography;
using ContentService.Application.Common;
using ContentService.Application.Models;
using ContentService.Application.Options;
using ContentService.Application.UseCases.Health;
using ContentService.Domain.Common;
using ContentService.Domain.Entities;
using ContentService.Domain.Enums;
using ContentService.Infrastructure.Persistence;
using ContentService.Infrastructure.Repositories;
using ContentService.Tests.Application.Persistence;
using ContentService.Tests.Common;
using MicrosoftOptions = Microsoft.Extensions.Options.Options;

namespace ContentService.Tests.Application;

/// <summary>
/// Integration tests for <see cref="GetStorageHealthUseCase"/> executed against a real SQL Server container.
/// </summary>
[Collection(MsSqlContentDatabaseCollection.COLLECTION_NAME)]
[Trait(TestTraits.CATEGORY_KEY, TestTraits.INTEGRATION)]
public sealed class GetStorageHealthUseCaseTests : IAsyncLifetime
{
        /// <summary>
        /// Valid storage node identifier used when seeding test images.
        /// </summary>
        private const string VALID_NODE_ID = "node-1";

        /// <summary>
        /// Valid actor identifier used for audit fields when seeding test images.
        /// </summary>
        private const string VALID_ACTOR_ID = "health-monitor";

        /// <summary>
        /// Small storage quota in bytes used to exercise exact percentage boundaries.
        /// </summary>
        private const long QUOTA_ONE_HUNDRED_BYTES = 100L;

        /// <summary>
        /// Standard storage quota in bytes used across threshold-mapping theory cases.
        /// </summary>
        private const long QUOTA_ONE_THOUSAND_BYTES = 1000L;

        /// <summary>
        /// Warning threshold percentage targeting the IEEE-754 binary64 boundary at 29/100.
        /// </summary>
        private const double WARNING_THRESHOLD_29 = 29.0;

        /// <summary>
        /// Critical utilization threshold percentage triggering <see cref="StorageHealthStatus.FullReadOnly"/>.
        /// </summary>
        private const double CRITICAL_THRESHOLD_95 = 95.0;

        /// <summary>
        /// Active image payload byte count producing 29% utilization against <see cref="QUOTA_ONE_HUNDRED_BYTES"/>.
        /// </summary>
        private const long EXACT_29_BYTES = 29L;

        /// <summary>
        /// Shared SQL Server test container fixture.
        /// </summary>
        private readonly MsSqlContentDatabaseFixture _fixture;

        /// <summary>
        /// Initializes a new instance of the <see cref="GetStorageHealthUseCaseTests"/> class.
        /// </summary>
        /// <param name="fixture">The shared SQL Server container fixture.</param>
        public GetStorageHealthUseCaseTests(MsSqlContentDatabaseFixture fixture)
        {
                ArgumentNullException.ThrowIfNull(fixture);
                this._fixture = fixture;
        }

        /// <summary>
        /// Clears all SQL Server tables before each test executes.
        /// </summary>
        /// <returns>A task representing asynchronous database cleanup.</returns>
        public ValueTask InitializeAsync() => new(this._fixture.ResetDatabaseAsync());

        /// <summary>
        /// Completes per-test cleanup without disposing the shared SQL Server container.
        /// </summary>
        /// <returns>A completed task.</returns>
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>
        /// Verifies mapping from SQL Server byte utilization across Healthy, Warning, and >= 100% FullReadOnly states.
        /// </summary>
        /// <param name="usedBytes">The seeded active image byte count.</param>
        /// <param name="expectedStatus">The expected <see cref="StorageHealthStatus"/> classification.</param>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Theory]
        [InlineData(0L, StorageHealthStatus.Healthy)]
        [InlineData(799L, StorageHealthStatus.Healthy)]
        [InlineData(800L, StorageHealthStatus.Warning)]
        [InlineData(949L, StorageHealthStatus.Warning)]
        [InlineData(950L, StorageHealthStatus.FullReadOnly)]
        [InlineData(1000L, StorageHealthStatus.FullReadOnly)]
        [InlineData(1200L, StorageHealthStatus.FullReadOnly)]
        public async Task ExecuteAsync_WhenQuotaPositive_MapsUtilizationThresholdsCorrectlyAsync(
                long usedBytes,
                StorageHealthStatus expectedStatus)
        {
                // Arrange
                // Skip seeding for 0-byte case to also verify empty-table SUM handling in SQL Server.
                if (usedBytes > 0)
                {
                        await this.SeedActiveImageOfSizeAsync(byteCount: usedBytes);
                }

                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);
                ContentStorageOptions options = new()
                {
                        TotalQuotaBytes = QUOTA_ONE_THOUSAND_BYTES,
                        WarningThresholdPercent = ApplicationConstants.DEFAULT_WARNING_THRESHOLD_PERCENT,
                        CriticalThresholdPercent = CRITICAL_THRESHOLD_95
                };
                GetStorageHealthUseCase useCase = new(
                        quotaMonitor: quotaMonitor,
                        options: MicrosoftOptions.Create(options));

                // Act
                StorageHealthResult result = await useCase.ExecuteAsync(cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(expectedStatus, result.Status);
                Assert.Equal(usedBytes, result.UsedBytes);
                Assert.Equal(QUOTA_ONE_THOUSAND_BYTES, result.QuotaBytes);
        }

        /// <summary>
        /// Verifies that exact decimal threshold 29/100 (28.999999999999996 in IEEE-754 binary64) classifies accurately as <see cref="StorageHealthStatus.Warning"/> from SQL Server usage.
        /// </summary>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Fact]
        public async Task ExecuteAsync_WhenAtExactIeee754Boundary29Percent_ClassifiesAsWarningAsync()
        {
                // Arrange
                // Seed 29 bytes out of 100 so decimal math avoids binary64 underflow at 28.999999999999996%.
                await this.SeedActiveImageOfSizeAsync(byteCount: EXACT_29_BYTES);
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);
                ContentStorageOptions options = new()
                {
                        TotalQuotaBytes = QUOTA_ONE_HUNDRED_BYTES,
                        WarningThresholdPercent = WARNING_THRESHOLD_29,
                        CriticalThresholdPercent = CRITICAL_THRESHOLD_95
                };
                GetStorageHealthUseCase useCase = new(
                        quotaMonitor: quotaMonitor,
                        options: MicrosoftOptions.Create(options));

                // Act
                StorageHealthResult result = await useCase.ExecuteAsync(cancellationToken: CancellationToken.None);

                // Assert
                Assert.Equal(StorageHealthStatus.Warning, result.Status);
                Assert.Equal(WARNING_THRESHOLD_29, result.UtilizationPercent);
        }

        /// <summary>
        /// Verifies that zero or negative quota returns <see cref="StorageHealthStatus.FullReadOnly"/> without division-by-zero against real SQL Server usage.
        /// </summary>
        /// <param name="quotaBytes">Zero or negative configured quota.</param>
        /// <returns>A task representing the asynchronous test execution.</returns>
        [Theory]
        [InlineData(0L)]
        [InlineData(-500L)]
        public async Task ExecuteAsync_WhenQuotaZeroOrNegative_ReturnsFullReadOnlyWithoutDivideByZeroAsync(long quotaBytes)
        {
                // Arrange
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerStorageQuotaMonitor quotaMonitor = new(dbContext: dbContext);
                ContentStorageOptions options = new() { TotalQuotaBytes = quotaBytes };
                GetStorageHealthUseCase useCase = new(
                        quotaMonitor: quotaMonitor,
                        options: MicrosoftOptions.Create(options));

                // Act
                StorageHealthResult result = await useCase.ExecuteAsync(cancellationToken: CancellationToken.None);

                // Assert
                // Non-positive quotas normalize reported QuotaBytes to 0 and force FullReadOnly at 100% utilization.
                Assert.Equal(StorageHealthStatus.FullReadOnly, result.Status);
                Assert.Equal(0L, result.UsedBytes);
                Assert.Equal(0L, result.QuotaBytes);
                Assert.Equal(ApplicationConstants.FULL_UTILIZATION_PERCENT, result.UtilizationPercent);
        }

        /// <summary>
        /// Seeds a single active image metadata and binary row with the specified byte length in SQL Server.
        /// </summary>
        /// <param name="byteCount">The exact payload size in bytes.</param>
        /// <returns>A task representing the asynchronous database insert.</returns>
        private async Task SeedActiveImageOfSizeAsync(long byteCount)
        {
                // Seed directly via SqlServerImageRepository to bypass UploadImageUseCase magic-byte/size validation for arbitrary byte counts.
                await using ContentDbContext dbContext = this._fixture.CreateDbContext();
                SqlServerImageRepository imageRepository = new(dbContext: dbContext);
                byte[] payload = new byte[byteCount];
                string hash = Convert.ToHexStringLower(SHA256.HashData(payload));
                ImageEntity image = ImageEntity.Create(
                        storageNodeId: VALID_NODE_ID,
                        fileName: "quota-seed.png",
                        contentType: DomainConstants.CONTENT_TYPE_PNG,
                        fileSizeBytes: byteCount,
                        sha256Hash: hash,
                        actorId: VALID_ACTOR_ID);
                ImageBinaryEntity binary = ImageBinaryEntity.Create(imageId: image.Id, data: payload);

                await imageRepository.AddAsync(metadata: image, binary: binary, cancellationToken: CancellationToken.None);
        }
}
