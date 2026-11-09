namespace dorks_and_dice_site.Tests;

/// <summary>
/// Serializes integration tests sharing PostgreSQL resources to prevent
/// cross-test data races and database initialization conflicts.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresIntegrationCollection
{
    public const string Name = "PostgreSQL integration";
}
