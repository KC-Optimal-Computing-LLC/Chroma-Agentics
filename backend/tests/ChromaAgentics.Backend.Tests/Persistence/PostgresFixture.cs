using ChromaAgentics.Backend.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace ChromaAgentics.Backend.Tests.Persistence;

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("chroma_agentics_tests")
        .WithUsername("chroma")
        .WithPassword("chroma_dev_password")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await ResetDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        await container.DisposeAsync();
    }

    public ChromaAgenticsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ChromaAgenticsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new ChromaAgenticsDbContext(options);
    }

    public async Task ResetDatabaseAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    internal TestBackendFactory CreateBackendFactory()
    {
        return new TestBackendFactory(configuration: new Dictionary<string, string?>
        {
            ["CHROMA_DATABASE_CONNECTION_STRING"] = ConnectionString,
            ["CHROMA_REQUIRE_POSTGRES"] = "true",
            ["CHROMA_REQUIRE_OLLAMA"] = "false"
        });
    }
}
