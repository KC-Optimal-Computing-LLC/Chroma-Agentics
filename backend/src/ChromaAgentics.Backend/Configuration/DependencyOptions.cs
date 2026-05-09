namespace ChromaAgentics.Backend.Configuration;

public sealed class DependencyOptions
{
    public const string DefaultOllamaBaseUrl = "http://localhost:11434";

    public string? DatabaseConnectionString { get; init; }
    public string OllamaBaseUrl { get; init; } = DefaultOllamaBaseUrl;
    public bool RequirePostgres { get; init; } = true;
    public bool RequireOllama { get; init; }

    public static DependencyOptions FromConfiguration(IConfiguration configuration)
    {
        return new DependencyOptions
        {
            DatabaseConnectionString = configuration["CHROMA_DATABASE_CONNECTION_STRING"],
            OllamaBaseUrl = ConfigurationParsers.GetString(
                configuration,
                "CHROMA_OLLAMA_BASE_URL",
                DefaultOllamaBaseUrl),
            RequirePostgres = ConfigurationParsers.GetBool(configuration, "CHROMA_REQUIRE_POSTGRES", true),
            RequireOllama = ConfigurationParsers.GetBool(configuration, "CHROMA_REQUIRE_OLLAMA", false)
        };
    }
}
