using ContentService.Application.Interfaces;

namespace ContentService.Tests.Application.Persistence;

/// <summary>
/// Test-only spy decorator around <see cref="IImageBinaryStore"/> counting <see cref="OpenReadAsync"/> invocations.
/// </summary>
internal sealed class CountingImageBinaryStore : IImageBinaryStore
{
        /// <summary>
        /// Decorated production binary store implementation.
        /// </summary>
        private readonly IImageBinaryStore _innerStore;

        /// <summary>
        /// Initializes a new instance of the <see cref="CountingImageBinaryStore"/> class.
        /// </summary>
        /// <param name="innerStore">The underlying binary store implementation.</param>
        public CountingImageBinaryStore(IImageBinaryStore innerStore)
        {
                ArgumentNullException.ThrowIfNull(innerStore);
                this._innerStore = innerStore;
        }

        /// <summary>
        /// Gets the number of times <see cref="OpenReadAsync"/> has been invoked.
        /// </summary>
        public int OpenReadCallCount { get; private set; }

        /// <inheritdoc />
        public Task<Stream?> OpenReadAsync(Guid imageId, CancellationToken cancellationToken)
        {
                this.OpenReadCallCount++;
                return this._innerStore.OpenReadAsync(imageId: imageId, cancellationToken: cancellationToken);
        }
}

