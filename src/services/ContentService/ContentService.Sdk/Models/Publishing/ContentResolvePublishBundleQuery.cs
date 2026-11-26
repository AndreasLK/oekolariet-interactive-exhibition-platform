namespace ContentService.Sdk.Models;

/// <summary>
/// Query payload for resolving and checksum-sealing a self-contained offline kiosk publish bundle.
/// </summary>
public sealed record class ContentResolvePublishBundleQuery
{
        /// <summary>
        /// Gets the optional template identifier whose schema and image references should be bundled alongside <see cref="ImplementationJson"/>.
        /// </summary>
        public Guid? TemplateId { get; init; }

        /// <summary>
        /// Gets the raw exhibition implementation JSON authored in EditorService.
        /// </summary>
        public required string ImplementationJson { get; init; }
}

