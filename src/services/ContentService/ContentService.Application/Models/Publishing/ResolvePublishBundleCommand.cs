namespace ContentService.Application.Models;

/// <summary>
/// Command payload for resolving and checksum-sealing a self-contained offline publish bundle for kiosk servers.
/// </summary>
public sealed record class ResolvePublishBundleCommand
{
        /// <summary>
        /// Gets the optional template identifier whose schema and image references should be bundled alongside <see cref="ImplementationJson"/>.
        /// </summary>
        /// <remarks>
        /// Nullable because bespoke one-off exhibition implementations can publish directly with custom layout JSON without referencing a saved template.
        /// </remarks>
        public Guid? TemplateId { get; init; }

        /// <summary>
        /// Gets the raw exhibition implementation JSON authored in UI Editor.
        /// </summary>
        public required string ImplementationJson { get; init; }
}

