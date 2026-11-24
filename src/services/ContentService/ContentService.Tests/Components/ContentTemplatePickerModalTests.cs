using ALKsPersonalUI;
using Bunit;
using ContentService.Components.Components;
using ContentService.Sdk.Clients;
using ContentService.Sdk.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace ContentService.Tests.Components;

/// <summary>
/// bUnit component tests verifying deferred selection and explicit confirmation in <see cref="ContentTemplatePickerModal"/>.
/// </summary>
public sealed class ContentTemplatePickerModalTests : BunitContext
{
        /// <summary>
        /// Verifies that clicking a template preview or selection button keeps the modal open until the user clicks Vælg.
        /// </summary>
        [Fact]
        public void SelectionToggle_WhenClicked_KeepsModalOpenUntilConfirmButtonClicked()
        {
                // Arrange
                ContentTemplateMetadata sampleTemplate = new()
                {
                        Id = Guid.NewGuid(),
                        ExternalId = "tpl-affaldsquiz",
                        Name = "Affaldsquiz",
                        ComponentType = ContentTemplateComponentType.InfoScreen,
                        SchemaVersion = 1,
                        SchemaJson = "{\"Type\":\"Column\",\"Children\":[]}",
                        IsArchived = false,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UpdatedAt = DateTimeOffset.UtcNow,
                        ConcurrencyStamp = Guid.NewGuid()
                };
                ContentTemplateMetadata? confirmedTemplate = null;
                bool isOpen = true;

                this.JSInterop.Mode = JSRuntimeMode.Loose;
                this.Services.AddPersonalUILibrary(configure: static _ => { });
                this.Services.AddSingleton<IContentServiceTemplateClient>(implementationInstance: new StubTemplateClient(template: sampleTemplate));

                IRenderedComponent<ContentTemplatePickerModal> cut = this.Render<ContentTemplatePickerModal>(
                        parameterBuilder: parameters => parameters
                                .Add(parameterSelector: component => component.IsOpen, value: true)
                                .Add(
                                        parameterSelector: component => component.IsOpenChanged,
                                        value: EventCallback.Factory.Create<bool>(receiver: this, callback: updated => isOpen = updated))
                                .Add(
                                        parameterSelector: component => component.OnTemplateSelected,
                                        value: EventCallback.Factory.Create<ContentTemplateMetadata>(receiver: this, callback: template => confirmedTemplate = template)));

                AngleSharp.Dom.IElement confirmButtonBefore = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Vælg");
                Assert.True(condition: confirmButtonBefore.HasAttribute(name: "disabled"));

                // Act 1: click the selection dot button on the template card
                AngleSharp.Dom.IElement selectDotButton = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() is "○" or "✓");
                selectDotButton.Click();

                // Assert 1: modal stays open and callback has not fired yet
                Assert.True(condition: isOpen);
                Assert.Null(@object: confirmedTemplate);

                AngleSharp.Dom.IElement confirmButtonAfter = cut.FindAll(cssSelector: "button")
                        .First(predicate: button => button.TextContent.Trim() == "Vælg");
                Assert.False(condition: confirmButtonAfter.HasAttribute(name: "disabled"));

                // Act 2: click Vælg to confirm
                confirmButtonAfter.Click();

                // Assert 2: callback fires and modal closes
                Assert.NotNull(@object: confirmedTemplate);
                Assert.Equal(expected: sampleTemplate.Id, actual: confirmedTemplate.Id);
                Assert.False(condition: isOpen);
        }

        private sealed class StubTemplateClient(ContentTemplateMetadata template) : IContentServiceTemplateClient
        {
                public Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
                        int page = 1,
                        int pageSize = 25,
                        ContentTemplateComponentType? componentType = null,
                        string? searchTerm = null,
                        CancellationToken cancellationToken = default) =>
                        Task.FromResult(result: new ContentPagedResult<ContentTemplateMetadata>
                        {
                                Items = [template],
                                Page = page,
                                PageSize = pageSize,
                                TotalCount = 1
                        });

                public Task<ContentPagedResult<ContentTemplateMetadata>> ListTemplatesAsync(
                        ContentTemplateQuery query,
                        CancellationToken cancellationToken = default) =>
                        Task.FromResult(result: new ContentPagedResult<ContentTemplateMetadata>
                        {
                                Items = [template],
                                Page = query.Page,
                                PageSize = query.PageSize,
                                TotalCount = 1
                        });

                public Task<ContentTemplateMetadata> GetTemplateAsync(Guid templateId, CancellationToken cancellationToken = default) =>
                        Task.FromResult(result: template);

                public Task<ContentTemplateMetadata> CreateTemplateAsync(CreateContentTemplateRequest request, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task<ContentTemplateMetadata> UpdateTemplateAsync(Guid templateId, UpdateContentTemplateRequest request, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();

                public Task DeleteTemplateAsync(Guid templateId, string? actorId = null, CancellationToken cancellationToken = default) =>
                        throw new NotSupportedException();
        }
}
