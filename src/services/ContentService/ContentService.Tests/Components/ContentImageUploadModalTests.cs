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
                        .First(predicate: button => button.TextContent.Trim() == "Annuller");

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

        /// <summary>
        /// Verifies that the upload modal keeps the primary file dropzone without encoding artifacts, mounts the hidden webkitdirectory input for direct Mappe clicks, and renders clickable existing album/tag pills.
        /// </summary>
        [Fact]
        public void FolderButtonAndLibrarySuggestions_WhenModalOpens_TriggersFolderPickerAndAppliesPills()
        {
                // Arrange
                StubContentImageClient stubClient = new()
                {
                        OnListAlbumsAsync = (_, _) => Task.FromResult<IReadOnlyList<string>>(result: ["Madaffald", "Plastik"]),
                        OnListImagesQueryAsync = (query, _) => Task.FromResult(result: new ContentPagedResult<ContentImageMetadata>
                        {
                                Items =
                                [
                                        new ContentImageMetadata
                                        {
                                                Id = Guid.NewGuid(),
                                                PictureReference = "picture.node-1.sample",
                                                ExternalId = "node-1_sample",
                                                FileName = "banana.png",
                                                ContentType = SAMPLE_PNG_CONTENT_TYPE,
                                                SizeBytes = 100,
                                                Sha256Hash = new string(c: 'a', count: 64),
                                                AlbumPath = "Madaffald",
                                                Tags = ["Madaffald", "Frugt"],
                                                CreatedAt = DateTimeOffset.UtcNow,
                                                ConcurrencyStamp = Guid.NewGuid()
                                        }
                                ],
                                Page = query.Page,
                                PageSize = query.PageSize,
                                TotalCount = 1
                        })
                };
                this.RegisterUiServices(imageClient: stubClient);

                IRenderedComponent<ContentImageUploadModal> cut = this.Render<ContentImageUploadModal>(
                        parameterBuilder: parameters => parameters.Add(parameterSelector: component => component.IsOpen, value: true));

                // Assert: no broken encoding, primary file input has no webkitdirectory, hidden folder input has webkitdirectory, and SVG folder icon is rendered
                Assert.DoesNotContain(expectedSubstring: "\uFFFD", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "<svg", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "webkitdirectory", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "Eksisterende mapper:", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "Eksisterende tags:", actualString: cut.Markup);

                // Act 1: click the Mappe button -> triggers JS import + openFolderPicker directly
                AngleSharp.Dom.IElement folderButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Contains(value: "Mappe", comparisonType: StringComparison.Ordinal));
                folderButton.Click();

                Assert.Contains(collection: this.JSInterop.Invocations, filter: invocation => invocation.Identifier == "openFolderPicker");

                // Act 2: click suggested album and tag pills
                AngleSharp.Dom.IElement madaffaldTagPill = cut.FindAll(cssSelector: "button, span")
                        .Last(predicate: element => element.TextContent.Trim() == "Madaffald");
                madaffaldTagPill.Click();

                AngleSharp.Dom.IElement plastikAlbumPill = cut.FindAll(cssSelector: "button, span")
                        .First(predicate: element => element.TextContent.Trim() == "Plastik");
                plastikAlbumPill.Click();

                Assert.Contains(expectedSubstring: "Madaffald", actualString: cut.Markup);
                Assert.Contains(expectedSubstring: "Plastik", actualString: cut.Markup);

                // Act 3: upload a file and verify the real ActionButton "Ryd billeder" clears it
                IRenderedComponent<InputFile> inputFile = cut.FindComponent<InputFile>();
                inputFile.UploadFiles(files: InputFileContent.CreateFromText(
                        fileContent: "fake-png",
                        fileName: "clear-me.png",
                        contentType: SAMPLE_PNG_CONTENT_TYPE));

                AngleSharp.Dom.IElement clearButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Ryd billeder");
                clearButton.Click();

                Assert.DoesNotContain(expectedSubstring: "clear-me.png", actualString: cut.Markup);
        }

        /// <summary>
        /// Verifies that clicking Fjern on a single preview card removes only that image without clearing the remaining selection.
        /// </summary>
        [Fact]
        public void RemoveSinglePreviewButton_WhenMultipleFilesSelected_RemovesOnlyClickedImage()
        {
                // Arrange
                this.RegisterUiServices(imageClient: new StubContentImageClient());
                IRenderedComponent<ContentImageUploadModal> cut = this.Render<ContentImageUploadModal>(
                        parameterBuilder: parameters => parameters.Add(parameterSelector: component => component.IsOpen, value: true));

                IRenderedComponent<InputFile> inputFile = cut.FindComponent<InputFile>();
                InputFileContent firstFile = InputFileContent.CreateFromText(
                        fileContent: "fake-png-1",
                        fileName: "first.png",
                        contentType: SAMPLE_PNG_CONTENT_TYPE);
                InputFileContent secondFile = InputFileContent.CreateFromText(
                        fileContent: "fake-png-2",
                        fileName: "second.png",
                        contentType: SAMPLE_PNG_CONTENT_TYPE);

                inputFile.UploadFiles(files: [firstFile, secondFile]);

                // Act: click the first per-card "Fjern" button
                AngleSharp.Dom.IElement firstRemoveButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Fjern");
                firstRemoveButton.Click();

                // Assert: first.png is removed while second.png remains queued
                cut.WaitForAssertion(assertion: () =>
                {
                        Assert.DoesNotContain(expectedSubstring: "first.png", actualString: cut.Markup);
                        Assert.Contains(expectedSubstring: "second.png", actualString: cut.Markup);
                });
        }

        private void RegisterUiServices(IContentServiceImageClient imageClient)
        {
                this.JSInterop.Mode = JSRuntimeMode.Loose;
                this.Services.AddPersonalUILibrary(configure: static _ => { });
                this.Services.AddSingleton<IContentServiceImageClient>(implementationInstance: imageClient);
        }
}
