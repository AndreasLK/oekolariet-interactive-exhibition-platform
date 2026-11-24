using ALKsPersonalUI;
using Bunit;
using ContentService.Components.Components;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using ContentService.Tests.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Tests.Components;

/// <summary>
/// bUnit component tests verifying deferred selection and explicit confirmation in <see cref="ContentImagePickerModal"/>.
/// </summary>
public sealed class ContentImagePickerModalTests : BunitContext
{
        /// <summary>
        /// Verifies that clicking the selection blue dot or tile keeps the modal open until the user clicks Vælg.
        /// </summary>
        [Fact]
        public void SelectionToggle_WhenClicked_KeepsModalOpenUntilConfirmButtonClicked()
        {
                // Arrange
                ContentImageMetadata sampleImage = CreateMetadata(fileName: "leaf.png");
                ContentImageMetadata? confirmedImage = null;
                bool isOpen = true;

                StubContentImageClient stubClient = new()
                {
                        OnListImagesQueryAsync = (query, _) => Task.FromResult(result: new ContentPagedResult<ContentImageMetadata>
                        {
                                Items = [sampleImage],
                                Page = query.Page,
                                PageSize = query.PageSize,
                                TotalCount = 1
                        })
                };

                this.JSInterop.Mode = JSRuntimeMode.Loose;
                this.Services.AddPersonalUILibrary(configure: static _ => { });
                this.Services.AddSingleton<IContentServiceImageClient>(implementationInstance: stubClient);

                IRenderedComponent<ContentImagePickerModal> cut = this.Render<ContentImagePickerModal>(
                        parameterBuilder: parameters => parameters
                                .Add(parameterSelector: component => component.IsOpen, value: true)
                                .Add(
                                        parameterSelector: component => component.IsOpenChanged,
                                        value: EventCallback.Factory.Create<bool>(receiver: this, callback: updated => isOpen = updated))
                                .Add(
                                        parameterSelector: component => component.OnImageSelected,
                                        value: EventCallback.Factory.Create<ContentImageMetadata>(receiver: this, callback: image => confirmedImage = image)));

                AngleSharp.Dom.IElement confirmButtonBefore = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Vælg");
                Assert.True(condition: confirmButtonBefore.HasAttribute(name: "disabled"));

                // Act 1: click the blue-dot selection button on the MediaTile
                AngleSharp.Dom.IElement blueDotButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.GetAttribute(name: "aria-label")?.Contains(value: "Select", comparisonType: StringComparison.OrdinalIgnoreCase) == true
                                || button.ClassList.Contains(token: "ui-media-tile-select"));
                blueDotButton.Click();

                // Assert 1: modal stays open and callback has not fired yet
                Assert.True(condition: isOpen);
                Assert.Null(@object: confirmedImage);

                AngleSharp.Dom.IElement confirmButtonAfter = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Vælg");
                Assert.False(condition: confirmButtonAfter.HasAttribute(name: "disabled"));

                // Act 2: click Vælg to confirm
                confirmButtonAfter.Click();

                // Assert 2: callback fires and modal closes
                Assert.NotNull(@object: confirmedImage);
                Assert.Equal(expected: sampleImage.Id, actual: confirmedImage.Id);
                Assert.False(condition: isOpen);
        }

        private static ContentImageMetadata CreateMetadata(string fileName)
        {
                Guid id = Guid.NewGuid();
                return new ContentImageMetadata
                {
                        Id = id,
                        PictureReference = $"picture.node-1.{id:D}",
                        ExternalId = $"node-1_{id:D}",
                        FileName = fileName,
                        ContentType = "image/png",
                        SizeBytes = 1024,
                        Sha256Hash = new string(c: 'a', count: 64),
                        CreatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };
        }
}

