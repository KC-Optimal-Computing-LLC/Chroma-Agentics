using ChromaAgentics.Backend.Health;

namespace ChromaAgentics.Backend.Tests;

public sealed class SecretRedactionTests
{
    [Fact]
    public async Task HealthResponses_DoNotExposeDevTokenOrDatabaseSecrets()
    {
        var errorWithSecrets =
            $"failed token={TestBackendFactory.ValidToken}; connection={TestBackendFactory.ConnectionString}; Password=swordfish";
        using var factory = new TestBackendFactory(
            new CountingPostgresHealthProbe(DependencyProbeResult.Unhealthy(errorWithSecrets)),
            new CountingOllamaHealthProbe(DependencyProbeResult.Healthy()));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/dependencies");
        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(TestBackendFactory.ValidToken, body, StringComparison.Ordinal);
        Assert.DoesNotContain(TestBackendFactory.ConnectionString, body, StringComparison.Ordinal);
        Assert.DoesNotContain("swordfish", body, StringComparison.Ordinal);
        Assert.Contains("[redacted]", body, StringComparison.Ordinal);
    }
}
