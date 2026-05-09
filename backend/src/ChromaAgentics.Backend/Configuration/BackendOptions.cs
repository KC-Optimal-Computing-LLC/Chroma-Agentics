using System.Net;

namespace ChromaAgentics.Backend.Configuration;

public sealed class BackendOptions
{
    public const string ServiceName = "chroma-agentics-backend";
    public const string DefaultHost = "localhost";
    public const int DefaultPort = 5127;
    public const string DefaultEnvironment = "Development";

    public string Host { get; init; } = DefaultHost;
    public int Port { get; init; } = DefaultPort;
    public string Environment { get; init; } = DefaultEnvironment;
    public string? DevAuthToken { get; init; }
    public bool AllowLanBinding { get; init; }

    public string ListenUrl => $"http://{FormatHostForUrl(Host)}:{Port}";

    public static BackendOptions FromConfiguration(IConfiguration configuration)
    {
        return new BackendOptions
        {
            Host = ConfigurationParsers.GetString(configuration, "CHROMA_BACKEND_HOST", DefaultHost),
            Port = ConfigurationParsers.GetInt(configuration, "CHROMA_BACKEND_PORT", DefaultPort),
            Environment = ConfigurationParsers.GetString(configuration, "CHROMA_BACKEND_ENVIRONMENT", DefaultEnvironment),
            DevAuthToken = configuration["CHROMA_DEV_AUTH_TOKEN"],
            AllowLanBinding = ConfigurationParsers.GetBool(configuration, "CHROMA_ALLOW_LAN_BINDING", false)
        };
    }

    public object ToRedactedStartupConfig(DependencyOptions dependencyOptions)
    {
        return new
        {
            Host,
            Port,
            Environment,
            AllowLanBinding,
            DevAuthTokenConfigured = !string.IsNullOrWhiteSpace(DevAuthToken),
            DatabaseConnectionStringConfigured = !string.IsNullOrWhiteSpace(dependencyOptions.DatabaseConnectionString),
            dependencyOptions.RequirePostgres,
            OllamaBaseUrlConfigured = !string.IsNullOrWhiteSpace(dependencyOptions.OllamaBaseUrl),
            OllamaBaseUrl = RedactUrl(dependencyOptions.OllamaBaseUrl),
            dependencyOptions.RequireOllama
        };
    }

    public static bool IsLoopbackHost(string host)
    {
        var normalized = host.Trim().Trim('[', ']');

        if (normalized.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IPAddress.TryParse(normalized, out var address))
        {
            return IPAddress.IsLoopback(address);
        }

        return false;
    }

    private static string FormatHostForUrl(string host)
    {
        var normalized = host.Trim();
        return normalized.Contains(':') && !normalized.StartsWith('[')
            ? $"[{normalized}]"
            : normalized;
    }

    private static string? RedactUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return url;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "[configured-invalid-url]";
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Path = string.Empty,
            Query = string.Empty
        };

        return builder.Uri.ToString();
    }
}

public static class BackendOptionsValidator
{
    public static void Validate(BackendOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            throw new InvalidOperationException("CHROMA_BACKEND_HOST must not be empty.");
        }

        if (options.Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("CHROMA_BACKEND_PORT must be between 1 and 65535.");
        }

        if (!options.AllowLanBinding && !BackendOptions.IsLoopbackHost(options.Host))
        {
            throw new InvalidOperationException(
                "CHROMA_BACKEND_HOST must be localhost or loopback unless CHROMA_ALLOW_LAN_BINDING=true.");
        }
    }
}

internal static class ConfigurationParsers
{
    public static string GetString(IConfiguration configuration, string key, string defaultValue)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    public static int GetInt(IConfiguration configuration, string key, int defaultValue)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"{key} must be an integer.");
    }

    public static bool GetBool(IConfiguration configuration, string key, bool defaultValue)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return bool.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"{key} must be true or false.");
    }
}
