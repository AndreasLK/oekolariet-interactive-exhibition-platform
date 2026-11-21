namespace ContentService.Tests.Application.Persistence;

/// <summary>
/// xUnit collection fixture binding database-backed application test classes to one shared <see cref="MsSqlContentDatabaseFixture"/>.
/// </summary>
[CollectionDefinition(COLLECTION_NAME)]
public sealed class MsSqlContentDatabaseCollection : ICollectionFixture<MsSqlContentDatabaseFixture>
{
        /// <summary>
        /// Collection name referenced by test classes requiring real SQL Server persistence.
        /// </summary>
        public const string COLLECTION_NAME = "MsSqlContentDatabase";
}

