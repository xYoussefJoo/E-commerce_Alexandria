using ECommerceMVC.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ECommerceMVC.Tests;

// Each instance gets its own uniquely-named SQLite in-memory database (shared-cache mode),
// so CreateContext() can be called multiple times to hand out independent DbContext instances
// that all see the same data - needed to simulate separate concurrent requests. The keep-alive
// connection stays open for the lifetime of the instance because an in-memory SQLite database
// is destroyed the moment its last connection closes.
public sealed class TestDatabase : IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _keepAliveConnection;

    public TestDatabase()
    {
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"file:{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connectionString)
            .AddInterceptors(interceptors)
            .Options;
        return new ApplicationDbContext(options);
    }

    public void Dispose() => _keepAliveConnection.Dispose();
}
