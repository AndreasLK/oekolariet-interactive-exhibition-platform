namespace HighscoreService.Tests.Hub
{
        /// <summary>
        /// xUnit collection definition sharing a single SQL Server Testcontainers instance across Hub integration tests.
        /// </summary>
        [CollectionDefinition(COLLECTION_NAME)]
        public sealed class MsSqlHighscoreDatabaseCollection : ICollectionFixture<MsSqlHighscoreDatabaseFixture>
        {
                /// <summary>
                /// Collection name referenced by Hub SQL Server integration test classes.
                /// </summary>
                public const string COLLECTION_NAME = "MsSqlHighscoreDatabase";
        }
}

