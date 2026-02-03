using BudgetWise.Infrastructure.Database;
using Xunit;

namespace BudgetWise.Infrastructure.Tests;

/// <summary>
/// Shared fixture that provides an in-memory SQLite database for tests.
/// </summary>
public sealed class TestDatabaseFixture : IDisposable
{
    public SqliteConnectionFactory ConnectionFactory { get; }

    public TestDatabaseFixture()
    {
        ConnectionFactory = SqliteConnectionFactory.CreateInMemory();
        ConnectionFactory.InitializeDatabaseAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        ConnectionFactory.Dispose();
    }
}

/// <summary>
/// Collection definition for sharing the database fixture.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<TestDatabaseFixture>
{
}
