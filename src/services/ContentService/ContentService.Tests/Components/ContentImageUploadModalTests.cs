using System.Text;
using ALKsPersonalUI;
using ALKsPersonalUI.Overlays;
using Bunit;
using ContentService.Components.Components;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using ContentService.Tests.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Tests.Components;

/// <summary>
/// bUnit component tests verifying batch upload recovery, modal lock during active uploads, and album/tag forwarding in <see cref="ContentImageUploadModal"/>.
/// </summary>
public sealed class ContentImageUploadModalTests : BunitContext
{
        private const string SAMPLE_PNG_CONTENT_TYPE = "image/png";

        /// <summary>
        /// Verifies that a partial batch failure reports a warning toast, forwards default album path and tags, and completes with successful uploads.
        /// </summary>
        [Fact]
        public void UploadBatch_WhenOneFileFailsWithNetworkError_ReportsWarningAndForwardsAlbumAndTags()
        {
                // Arrange
                List<ContentImageMetadata> completedUploads = [];
                List<UploadImageRequest> capturedRequests = [];
                bool closedRequested = false;
                StubContentImageClient stubClient = new()
                {
                        OnUploadImageAsync = (request, _) =>
                        {
                                capturedRequests.Add(item: request);
                                if (request.FileName == "broken.png")
                                {
                                        throw new HttpRequestException(message: "Connection reset");
                                }

                                Guid id = Guid.NewGuid();
                                return Task.FromResult(result: new ContentImageMetadata
                                {
                                        Id = id,
                                        PictureReference = $"picture.node-1.{id:D}",
                                        ExternalId = $"node-1_{id:D}",
                                        FileName = request.FileName,
                                        ContentType = request.ContentType,
                                        SizeBytes = 12,
                                        Sha256Hash = new string(c: 'e', count: 64),
                                        AlbumPath = request.AlbumPath,
                                        Tags = ["banana"],
                                        CreatedAt = DateTimeOffset.UtcNow,
                                        ConcurrencyStamp = Guid.NewGuid()
                                });
                        }
                };
                this.RegisterUiServices(imageClient: stubClient);
                ToastService toasts = this.Services.GetRequiredService<ToastService>();

                IRenderedComponent<ContentImageUploadModal> cut = this.Render<ContentImageUploadModal>(
                        parameterBuilder: parameters => parameters
                                .Add(parameterSelector: component => component.IsOpen, value: true)
                                .Add(parameterSelector: component => component.DefaultAlbumPath, value: "Madaffald")
                                .Add(parameterSelector: component => component.LockAlbumPath, value: true)
                                .Add(
                                        parameterSelector: component => component.IsOpenChanged,
                                        value: EventCallback.Factory.Create<bool>(receiver: this, callback: isOpen => closedRequested = !isOpen))
                                .Add(
                                        parameterSelector: component => component.OnUploadCompleted,
                                        value: EventCallback.Factory.Create<IReadOnlyList<ContentImageMetadata>>(
                                                receiver: this,
                                                callback: items => completedUploads.AddRange(collection: items))));

                IRenderedComponent<InputFile> inputFile = cut.FindComponent<InputFile>();
                InputFileContent validFile = InputFileContent.CreateFromText(
                        fileContent: "fake-png-bytes",
                        fileName: "valid.png",
                        contentType: SAMPLE_PNG_CONTENT_TYPE);
                InputFileContent brokenFile = InputFileContent.CreateFromText(
                        fileContent: "fake-png-bytes",
                        fileName: "broken.png",
                        contentType: SAMPLE_PNG_CONTENT_TYPE);

                // Act
                inputFile.UploadFiles(files: [validFile, brokenFile]);
                AngleSharp.Dom.IElement uploadButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Upload");
                uploadButton.Click();

                // Assert
                cut.WaitForAssertion(assertion: () =>
                {
                        Assert.Single(collection: completedUploads);
                        Assert.Equal(expected: "valid.png", actual: completedUploads[0].FileName);
                        Assert.All(collection: capturedRequests, action: req => Assert.Equal(expected: "Madaffald", actual: req.AlbumPath));
                        Assert.True(condition: closedRequested);
                        IReadOnlyList<ToastMessage> messages = toasts.GetToasts();
                        Assert.Contains(collection: messages, filter: toast => toast.Variant == ToastVariant.Danger);
                        Assert.Contains(collection: messages, filter: toast => toast.Variant == ToastVariant.Warning);
                });
        }

        /// <summary>
        /// Verifies that cancelling the modal while an upload is in flight is ignored until the stream completes.
        /// </summary>
        [Fact]
        public async Task CancelButton_WhileUploadInProgress_DoesNotCloseModalAsync()
        {
                // Arrange
                TaskCompletionSource<ContentImageMetadata> uploadGate = new();
                bool isOpen = true;
                StubContentImageClient stubClient = new()
                {
                        OnUploadImageAsync = (_, _) => uploadGate.Task
                };
                this.RegisterUiServices(imageClient: stubClient);

                IRenderedComponent<ContentImageUploadModal> cut = this.Render<ContentImageUploadModal>(
                        parameterBuilder: parameters => parameters
                                .Add(parameterSelector: component => component.IsOpen, value: true)
                                .Add(
                                        parameterSelector: component => component.IsOpenChanged,
                                        value: EventCallback.Factory.Create<bool>(receiver: this, callback: updated => isOpen = updated)));

                IRenderedComponent<InputFile> inputFile = cut.FindComponent<InputFile>();
                inputFile.UploadFiles(files: InputFileContent.CreateFromBinary(
                        fileContent: Encoding.UTF8.GetBytes(s: "png"),
                        fileName: "slow.png",
                        contentType: SAMPLE_PNG_CONTENT_TYPE));

                AngleSharp.Dom.IElement uploadButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Upload");
                AngleSharp.Dom.IElement cancelButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Cancel");

                // Act
                Task clickUploadTask = uploadButton.ClickAsync(eventArgs: new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
                cancelButton.Click();

                // Assert
                Assert.True(condition: isOpen);

                Guid id = Guid.NewGuid();
                uploadGate.SetResult(result: new ContentImageMetadata
                {
                        Id = id,
                        PictureReference = $"picture.node-1.{id:D}",
                        ExternalId = $"node-1_{id:D}",
                        FileName = "slow.png",
                        ContentType = SAMPLE_PNG_CONTENT_TYPE,
                        SizeBytes = 3,
                        Sha256Hash = new string(c: 'f', count: 64),
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                });
                await clickUploadTask;
        }

        private void RegisterUiServices(IContentServiceImageClient imageClient)
        {
                this.JSInterop.Mode = JSRuntimeMode.Loose;
                this.Services.AddPersonalUILibrary(configure: static _ => { });
                this.Services.AddSingleton<IContentServiceImageClient>(implementationInstance: imageClient);
        }
}
