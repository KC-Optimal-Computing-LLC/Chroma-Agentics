using System.Text.Json.Serialization;

namespace ChromaAgentics.Backend.Health;

public static class HealthStatus
{
    public const string Healthy = "healthy";
    public const string Degraded = "degraded";
    public const string Unhealthy = "unhealthy";
    public const string NotConfigured = "not_configured";
}

public sealed class HealthResponse
{
    public required string Status { get; init; }
    public required string Service { get; init; }
    public required DateTime TimestampUtc { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DependencyHealthStatus>? Dependencies { get; init; }

    public static HealthResponse Live(DateTimeOffset timestampUtc)
    {
        return new HealthResponse
        {
            Status = HealthStatus.Healthy,
            Service = Configuration.BackendOptions.ServiceName,
            TimestampUtc = timestampUtc.UtcDateTime
        };
    }
}

public sealed class DependencyHealthStatus
{
    public required string Name { get; init; }
    public required string Status { get; init; }
    public required bool Required { get; init; }
    public required DateTime CheckedAtUtc { get; init; }
    public string? Error { get; init; }
}

public sealed record DependencyProbeResult(string Status, string? Error)
{
    public static DependencyProbeResult Healthy() => new(HealthStatus.Healthy, null);

    public static DependencyProbeResult Unhealthy(string error) => new(HealthStatus.Unhealthy, error);

    public static DependencyProbeResult NotConfigured(string error) => new(HealthStatus.NotConfigured, error);
}
