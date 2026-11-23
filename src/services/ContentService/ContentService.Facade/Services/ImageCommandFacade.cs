using ContentService.Application.Models;
using ContentService.Application.UseCases.Images;
using ContentService.Facade.Dtos;
using ContentService.Facade.Interfaces;
using ContentService.Facade.Mapping;

namespace ContentService.Facade.Services;

/// <summary>
/// Default <see cref="IImageCommandFacade"/> orchestrating state-mutating image use cases across the facade boundary.
/// </summary>
internal sealed class ImageCommandFacade : IImageCommandFacade
{
        /// <summary>
        /// Application use case handling image upload validation, quota checks, and persistence.
        /// </summary>
        private readonly UploadImageUseCase _uploadImageUseCase;

        /// <summary>
        /// Application use case handling optimistic-concurrency image metadata updates.
        /// </summary>
        private readonly UpdateImageMetadataUseCase _updateImageMetadataUseCase;

        /// <summary>
        /// Application use case handling reference-checked soft deletion of images.
        /// </summary>
        private readonly DeleteImageUseCase _deleteImageUseCase;

        /// <summary>
        /// Initializes a new instance of <see cref="ImageCommandFacade"/>.
        /// </summary>
        /// <param name="uploadImageUseCase">The upload image use case.</param>
        /// <param name="updateImageMetadataUseCase">The update image metadata use case.</param>
        /// <param name="deleteImageUseCase">The delete image use case.</param>
        public ImageCommandFacade(
                UploadImageUseCase uploadImageUseCase,
                UpdateImageMetadataUseCase updateImageMetadataUseCase,
                DeleteImageUseCase deleteImageUseCase)
        {
                ArgumentNullException.ThrowIfNull(uploadImageUseCase);
                ArgumentNullException.ThrowIfNull(updateImageMetadataUseCase);
                ArgumentNullException.ThrowIfNull(deleteImageUseCase);

                this._uploadImageUseCase = uploadImageUseCase;
                this._updateImageMetadataUseCase = updateImageMetadataUseCase;
                this._deleteImageUseCase = deleteImageUseCase;
        }

        /// <inheritdoc />
        public async Task<ImageMetadataDto> UploadImageAsync(
                UploadImageCommandDto command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(command);

                // Translate the transport-free facade DTO into the Application command before invoking domain validation and storage.
                UploadImageCommand appCommand = new()
                {
                        FileName = command.FileName,
                        ContentType = command.ContentType,
                        DeclaredLengthBytes = command.DeclaredLengthBytes,
                        Content = command.Content,
                        ActorId = command.ActorId,
                        AltText = command.AltText,
                        AlbumPath = command.AlbumPath,
                        Tags = command.Tags
                };

                ImageMetadataResult result = await this._uploadImageUseCase.ExecuteAsync(
                        command: appCommand,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }

        /// <inheritdoc />
        public async Task<ImageMetadataDto> UpdateImageMetadataAsync(
                UpdateImageMetadataCommandDto command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(command);

                UpdateImageMetadataCommand appCommand = new()
                {
                        ImageId = command.ImageId,
                        AltText = command.AltText,
                        Tags = command.Tags,
                        AlbumPath = command.AlbumPath,
                        ExpectedConcurrencyStamp = command.ExpectedConcurrencyStamp,
                        ActorId = command.ActorId
                };

                ImageMetadataResult result = await this._updateImageMetadataUseCase.ExecuteAsync(
                        command: appCommand,
                        cancellationToken: cancellationToken);

                return FacadeMapper.ToDto(result: result);
        }

        /// <inheritdoc />
        public async Task DeleteImageAsync(
                DeleteImageCommandDto command,
                CancellationToken cancellationToken = default)
        {
                ArgumentNullException.ThrowIfNull(command);

                DeleteImageCommand appCommand = new()
                {
                        ImageId = command.ImageId,
                        ActorId = command.ActorId
                };

                await this._deleteImageUseCase.ExecuteAsync(
                        command: appCommand,
                        cancellationToken: cancellationToken);
        }
}

