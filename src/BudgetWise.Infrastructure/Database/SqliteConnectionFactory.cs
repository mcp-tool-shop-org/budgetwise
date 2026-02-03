using Microsoft.Data.Sqlite;

namespace BudgetWise.Infrastructure.Database;

/// <summary>
/// Factory for creating and managing SQLite database connections.
/// </summary>
public sealed class SqliteConnectionFactory : IDisposable
{
    private readonly string _connectionString;
    private SqliteConnection? _connection;
    private bool _disposed;

    public SqliteConnectionFactory(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be empty.", nameof(databasePath));

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public static SqliteConnectionFactory CreateInMemory()
    {
        return new SqliteConnectionFactory(":memory:");
    }

    public async Task<SqliteConnection> GetConnectionAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is null)
        {
            _connection = new SqliteConnection(_connectionString);
            await _connection.OpenAsync(ct);
        }
        else if (_connection.State != System.Data.ConnectionState.Open)
        {
            await _connection.OpenAsync(ct);
        }

        return _connection;
    }

    public SqliteConnection GetConnection()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_connection is null)
        {
            _connection = new SqliteConnection(_connectionString);
            _connection.Open();
        }
        else if (_connection.State != System.Data.ConnectionState.Open)
        {
            _connection.Open();
        }

        return _connection;
    }

    public async Task InitializeDatabaseAsync(CancellationToken ct = default)
    {
        var connection = await GetConnectionAsync(ct);

        foreach (var tableScript in DatabaseSchema.AllTables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = tableScript;
            await command.ExecuteNonQueryAsync(ct);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _connection?.Dispose();
        _connection = null;
        _disposed = true;
    }
}
