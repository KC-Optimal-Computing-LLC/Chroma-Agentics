using ChromaAgentics.Backend.Configuration;
using ChromaAgentics.Backend.Health;
using ChromaAgentics.Backend.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChromaAgentics.Backend.Tests;

internal sealed class TestBackendFactory : WebApplicationFactory<Program>
{
    public const string ValidToken = "test-dev-token";
    public const string ConnectionString = "Host=localhost;Username=chroma;Password=swordfish;Database=chroma_agentics";

    private readonly Dictionary<string, string?> configuration;
    private readonly TimeProvider timeProvider;

    public TestBackendFactory(
        CountingPostgresHealthProbe? postgresProbe = null,
        CountingOllamaHealthProbe? ollamaProbe = null,
        IReadOnlyDictionary<string, string?>? configuration = null)
    {
        PostgresProbe = postgresProbe ?? new CountingPostgresHealthProbe(DependencyProbeResult.Healthy());
        OllamaProbe = ollamaProbe ?? new CountingOllamaHealthProbe(DependencyProbeResult.Healthy());
        this.configuration = DefaultConfiguration();

        if (configuration is not null)
        {
            foreach (var pair in configuration)
            {
                this.configuration[pair.Key] = pair.Value;
            }
        }

        timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 5, 9, 20, 0, 0, TimeSpan.Zero));
    }

    public CountingPostgresHealthProbe PostgresProbe { get; }

    public CountingOllamaHealthProbe OllamaProbe { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(configuration);
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPostgresHealthProbe>();
            services.RemoveAll<IOllamaHealthProbe>();
            services.RemoveAll<TimeProvider>();
            services.RemoveAll<BackendOptions>();
            services.RemoveAll<DependencyOptions>();
            services.RemoveAll<DbContextOptions<ChromaAgenticsDbContext>>();
            services.RemoveAll<ChromaAgenticsDbContext>();

            var testConfiguration = new ConfigurationBuilder()
                .AddInMemoryCollection(configuration)
                .Build();

            services.AddSingleton<IPostgresHealthProbe>(PostgresProbe);
            services.AddSingleton<IOllamaHealthProbe>(OllamaProbe);
            services.AddSingleton(timeProvider);
            services.AddSingleton(BackendOptions.FromConfiguration(testConfiguration));
            services.AddSingleton(DependencyOptions.FromConfiguration(testConfiguration));
            services.AddDbContext<ChromaAgenticsDbContext>(options =>
            {
                options.UseNpgsql(configuration["CHROMA_DATABASE_CONNECTION_STRING"]);
            });
        });
    }

    private static Dictionary<string, string?> DefaultConfiguration()
    {
        return new Dictionary<string, string?>
        {
            ["CHROMA_BACKEND_HOST"] = "localhost",
            ["CHROMA_BACKEND_PORT"] = "5127",
            ["CHROMA_BACKEND_ENVIRONMENT"] = "Testing",
            ["CHROMA_DATABASE_CONNECTION_STRING"] = ConnectionString,
            ["CHROMA_OLLAMA_BASE_URL"] = "http://localhost:11434",
            ["CHROMA_REQUIRE_POSTGRES"] = "true",
            ["CHROMA_REQUIRE_OLLAMA"] = "false",
            ["CHROMA_DEV_AUTH_TOKEN"] = ValidToken,
            ["CHROMA_ALLOW_LAN_BINDING"] = "false"
        };
    }
}

internal sealed class CountingPostgresHealthProbe(DependencyProbeResult result) : IPostgresHealthProbe
{
    public int CallCount { get; private set; }

    public Task<DependencyProbeResult> CheckAsync(DependencyOptions options, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(result);
    }
}

internal sealed class CountingOllamaHealthProbe(DependencyProbeResult result) : IOllamaHealthProbe
{
    public int CallCount { get; private set; }

    public Task<DependencyProbeResult> CheckAsync(DependencyOptions options, CancellationToken cancellationToken)
    {
        CallCount++;
        return Task.FromResult(result);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return utcNow;
    }
}
