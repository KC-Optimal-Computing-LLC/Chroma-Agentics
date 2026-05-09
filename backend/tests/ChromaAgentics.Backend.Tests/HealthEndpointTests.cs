using System.Net;
using System.Text.Json;
using ChromaAgentics.Backend.Configuration;
using ChromaAgentics.Backend.Health;

namespace ChromaAgentics.Backend.Tests;

public sealed class HealthEndpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Live_ReturnsHealthy_WithoutDependencyChecks()
    {
        using var factory = new TestBackendFactory(
            new CountingPostgresHealthProbe(DependencyProbeResult.Unhealthy("PostgreSQL is unavailable.")),
            new CountingOllamaHealthProbe(DependencyProbeResult.Unhealthy("Ollama is unavailable.")));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("dependencies", body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, factory.PostgresProbe.CallCount);
        Assert.Equal(0, factory.OllamaProbe.CallCount);

        var health = Deserialize<HealthResponse>(body);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.Equal(BackendOptions.ServiceName, health.Service);
    }

    [Fact]
    public async Task Ready_ReturnsHealthy_WithDependencyList_WhenRequiredDependenciesAreHealthy()
    {
        using var factory = new TestBackendFactory(
            new CountingPostgresHealthProbe(DependencyProbeResult.Healthy()),
            new CountingOllamaHealthProbe(DependencyProbeResult.Unhealthy("Ollama is unavailable.")));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var health = Deserialize<HealthResponse>(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthStatus.Healthy, health.Status);
        Assert.NotNull(health.Dependencies);
        Assert.Equal(2, health.Dependencies.Count);
        Assert.Contains(health.Dependencies, dependency =>
            dependency.Name == "ollama" &&
            dependency.Status == HealthStatus.Unhealthy &&
            dependency.Required == false);
    }

    [Fact]
    public async Task Ready_ReturnsServiceUnavailable_WhenRequiredPostgresIsUnavailable()
    {
        using var factory = new TestBackendFactory(
            new CountingPostgresHealthProbe(DependencyProbeResult.Unhealthy("PostgreSQL is unavailable.")),
            new CountingOllamaHealthProbe(DependencyProbeResult.Healthy()));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/ready");
        var health = Deserialize<HealthResponse>(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(HealthStatus.Unhealthy, health.Status);
        Assert.Contains(health.Dependencies!, dependency =>
            dependency.Name == "postgresql" &&
            dependency.Status == HealthStatus.Unhealthy &&
            dependency.Required);
    }

    [Fact]
    public async Task Dependencies_ReturnsStructuredDependencyStatuses()
    {
        using var factory = new TestBackendFactory(
            new CountingPostgresHealthProbe(DependencyProbeResult.Healthy()),
            new CountingOllamaHealthProbe(DependencyProbeResult.NotConfigured("Ollama base URL is not configured.")));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/dependencies");
        var health = Deserialize<HealthResponse>(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthStatus.Degraded, health.Status);
        Assert.NotNull(health.Dependencies);
        Assert.Contains(health.Dependencies, dependency =>
            dependency.Name == "postgresql" &&
            dependency.Status == HealthStatus.Healthy &&
            dependency.Required);
        Assert.Contains(health.Dependencies, dependency =>
            dependency.Name == "ollama" &&
            dependency.Status == HealthStatus.NotConfigured &&
            dependency.Required == false &&
            dependency.Error == "Ollama base URL is not configured.");
    }

    [Fact]
    public void BackendOptionsValidator_RejectsNonLoopbackBinding_WithoutLanOptIn()
    {
        var options = new BackendOptions
        {
            Host = "0.0.0.0",
            Port = 5127,
            Environment = "Testing",
            AllowLanBinding = false
        };

        var exception = Assert.Throws<InvalidOperationException>(() => BackendOptionsValidator.Validate(options));
        Assert.Contains("CHROMA_ALLOW_LAN_BINDING", exception.Message);
    }

    [Fact]
    public void BackendOptionsValidator_AllowsNonLoopbackBinding_WithLanOptIn()
    {
        var options = new BackendOptions
        {
            Host = "0.0.0.0",
            Port = 5127,
            Environment = "Testing",
            AllowLanBinding = true
        };

        BackendOptionsValidator.Validate(options);
    }

    private static T Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException("Response JSON could not be deserialized.");
    }
}
