namespace ContentService.Tests.Common;

/// <summary>
/// Shared xUnit trait keys and category names used to separate fast Docker-free tests from container-backed integration tests.
/// </summary>
public static class TestTraits
{
        /// <summary>
        /// Trait key identifying the test execution category.
        /// </summary>
        public const string CATEGORY_KEY = "Category";

        /// <summary>
        /// Trait value marking tests that require external infrastructure such as Docker and SQL Server.
        /// </summary>
        public const string INTEGRATION = "Integration";
}

