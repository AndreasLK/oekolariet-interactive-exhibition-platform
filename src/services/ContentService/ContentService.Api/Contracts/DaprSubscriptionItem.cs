namespace ContentService.Api.Contracts;

/// <summary>
/// Represents a Dapr Pub/Sub subscription entry returned by <c>/dapr/subscribe</c>.
/// </summary>
/// <param name="Pubsubname">The configured Dapr Pub/Sub component name.</param>
/// <param name="Topic">The topic name to subscribe to.</param>
/// <param name="Route">The versioned HTTP route that receives events for <paramref name="Topic"/>.</param>
internal sealed record class DaprSubscriptionItem(string Pubsubname, string Topic, string Route);

