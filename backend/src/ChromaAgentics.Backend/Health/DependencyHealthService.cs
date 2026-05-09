using System.Text.RegularExpressions;
using ChromaAgentics.Backend.Configuration;
using Npgsql;

namespace ChromaAgentics.Backend.Health;

public interface IDependencyHealthService
{
    Task<HealthResponse> GetReadinessAsync(CancellationToken cancellationToken);

    Task<HealthResponse> GetDependencyStatusAsync(CancellationToken cancellationToken);
}

public interface IPostgresHealthProbe
{
    Task<DependencyProbeResult> CheckAsync(DependencyOptions options, CancellationToken cancellationToken);
}

public interface IOllamaHealthProbe
{
    Task<DependencyProbeResult> CheckAsync(DependencyOptions options, CancellationToken cancellationToken);
}

public sealed class DependencyHealthService(
    BackendOptions backendOptions,
    DependencyOptions dependencyOptions,
    IPostgresHealthProbe postgresHealthProbe,
    IOllamaHealthProbe ollamaHealthProbe,
    TimeProvider timeProvider) : IDependencyHealthService
{
    public async Task<HealthResponse> GetReadinessAsync(CancellationToken cancellationToken)
    {
        var dependencies = await GetDependenciesAsync(cancellationToken);
        var requiredDependencyUnhealthy = dependencies.Any(IsRequiredAndUnhealthy);

        return new HealthResponse
        {
            Status = requiredDependencyUnhealthy ? HealthStatus.Unhealthy : HealthStatus.Healthy,
            Service = BackendOptions.ServiceName,
            TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
            Dependencies = dependencies
        };
    }

    public async Task<HealthResponse> GetDependencyStatusAsync(CancellationToken cancellationToken)
    {
        var dependencies = await GetDependenciesAsync(cancellationToken);
        var requiredDependencyUnhealthy = dependencies.Any(IsRequiredAndUnhealthy);
        var optionalDependencyUnhealthy = dependencies.Any(dependency => !dependency.Required && IsUnavailable(dependency));

        return new HealthResponse
        {
            Status = requiredDependencyUnhealthy
                ? HealthStatus.Unhealthy
                : optionalDependencyUnhealthy
                    ? HealthStatus.Degraded
                    : HealthStatus.Healthy,
            Service = BackendOptions.ServiceName,
            TimestampUtc = timeProvider.GetUtcNow().UtcDateTime,
            Dependencies = dependencies
        };
    }

    private async Task<IReadOnlyList<DependencyHealthStatus>> GetDependenciesAsync(CancellationToken cancellationToken)
    {
        var postgresql = postgresHealthProbe.CheckAsync(dependencyOptions, cancellationToken);
        var ollama = ollamaHealthProbe.CheckAsync(dependencyOptions, cancellationToken);

        await Task.WhenAll(postgresql, ollama);

        var postgresqlCheckedAt = timeProvider.GetUtcNow().UtcDateTime;
        var ollamaCheckedAt = timeProvider.GetUtcNow().UtcDateTime;

        return
        [
            ToStatus("postgresql", dependencyOptions.RequirePostgres, postgresqlCheckedAt, await postgresql),
            ToStatus("ollama", dependencyOptions.RequireOllama, ollamaCheckedAt, await ollama)
        ];
    }

    private DependencyHealthStatus ToStatus(
        string name,
        bool required,
        DateTime checkedAtUtc,
        DependencyProbeResult result)
    {
        return new DependencyHealthStatus
        {
            Name = name,
            Status = result.Status,
            Required = required,
            CheckedAtUtc = checkedAtUtc,
            Error = SecretRedactor.Redact(result.Error, backendOptions, dependencyOptions)
        };
    }

    private static bool IsRequiredAndUnhealthy(DependencyHealthStatus dependency)
    {
        return dependency.Required && IsUnavailable(dependency);
    }

    private static bool IsUnavailable(DependencyHealthStatus dependency)
    {
        return dependency.Status is HealthStatus.Unhealthy or HealthStatus.NotConfigured;
    }
}

public sealed class PostgresHealthProbe : IPostgresHealthProbe
{
    public async Task<DependencyProbeResult> CheckAsync(DependencyOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.DatabaseConnectionString))
        {
            return DependencyProbeResult.NotConfigured("PostgreSQL connection string is not configured.");
        }

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(options.DatabaseConnectionString)
            {
                Timeout = 2,
                CommandTimeout = 2
            };

            await using var connection = new NpgsqlConnection(builder.ConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);

            return DependencyProbeResult.Healthy();
        }
        catch (ArgumentException)
        {
            return DependencyProbeResult.Unhealthy("PostgreSQL connection string is invalid.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DependencyProbeResult.Unhealthy("PostgreSQL health check timed out.");
        }
        catch (Exception)
        {
            return DependencyProbeResult.Unhealthy("PostgreSQL is unavailable.");
        }
    }
}

public sealed class OllamaHealthProbe(HttpClient httpClient) : IOllamaHealthProbe
{
    public async Task<DependencyProbeResult> CheckAsync(DependencyOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.OllamaBaseUrl))
        {
            return DependencyProbeResult.NotConfigured("Ollama base URL is not configured.");
        }

        if (!Uri.TryCreate(options.OllamaBaseUrl, UriKind.Absolute, out var baseUri))
        {
            return DependencyProbeResult.Unhealthy("Ollama base URL is invalid.");
        }

        try
        {
            var endpoint = new Uri(baseUri, "/api/tags");
            using var response = await httpClient.GetAsync(endpoint, cancellationToken);

            return response.IsSuccessStatusCode
                ? DependencyProbeResult.Healthy()
                : DependencyProbeResult.Unhealthy($"Ollama returned HTTP {(int)response.StatusCode}.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return DependencyProbeResult.Unhealthy("Ollama health check timed out.");
        }
        catch (HttpRequestException)
        {
            return DependencyProbeResult.Unhealthy("Ollama is unavailable.");
        }
        catch (InvalidOperationException)
        {
            return DependencyProbeResult.Unhealthy("Ollama base URL is invalid.");
        }
    }
}

internal static partial class SecretRedactor
{
    public static string? Redact(string? value, BackendOptions backendOptions, DependencyOptions dependencyOptions)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = value;
        redacted = ReplaceIfPresent(redacted, backendOptions.DevAuthToken);
        redacted = ReplaceIfPresent(redacted, dependencyOptions.DatabaseConnectionString);

        if (!string.IsNullOrWhiteSpace(dependencyOptions.DatabaseConnectionString))
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(dependencyOptions.DatabaseConnectionString);
                redacted = ReplaceIfPresent(redacted, builder.Password);
            }
            catch (ArgumentException)
            {
                redacted = PasswordPattern().Replace(redacted, "$1=[redacted]");
            }
        }

        return PasswordPattern().Replace(redacted, "$1=[redacted]");
    }

    private static string ReplaceIfPresent(string value, string? secret)
    {
        return string.IsNullOrEmpty(secret)
            ? value
            : value.Replace(secret, "[redacted]", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?i)\b(password|pwd)\s*=\s*[^;\s]+")]
    private static partial Regex PasswordPattern();
}
