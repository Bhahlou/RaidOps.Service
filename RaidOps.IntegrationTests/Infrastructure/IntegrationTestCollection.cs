namespace RaidOps.IntegrationTests.Infrastructure;

/// <summary>
/// Declares a shared xUnit collection that provides a single <see cref="RaidOpsWebApplicationFactory"/>
/// (and therefore a single PostgreSQL Testcontainer) across all integration test classes.
/// Without this, xUnit creates one factory per class and Docker exhausts resources.
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<RaidOpsWebApplicationFactory>;
