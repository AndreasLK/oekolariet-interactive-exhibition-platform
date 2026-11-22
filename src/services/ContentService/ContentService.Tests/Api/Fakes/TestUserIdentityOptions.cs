namespace ContentService.Tests.Api.Fakes;

/// <summary>
/// Holds the authenticated identity name injected into test requests.
/// </summary>
internal sealed record class TestUserIdentityOptions(string UserName);

