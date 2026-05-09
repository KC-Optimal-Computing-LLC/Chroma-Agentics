using System.Text.Json;
using ChromaAgentics.Backend.Configuration;
using ChromaAgentics.Backend.Health;
using ChromaAgentics.Backend.Streaming;

var builder = WebApplication.CreateBuilder(args);

var backendOptions = BackendOptions.FromConfiguration(builder.Configuration);
var dependencyOptions = DependencyOptions.FromConfiguration(builder.Configuration);

BackendOptionsValidator.Validate(backendOptions);

builder.WebHost.UseUrls(backendOptions.ListenUrl);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(backendOptions);
builder.Services.AddSingleton(dependencyOptions);
builder.Services.AddSingleton<IPostgresHealthProbe, PostgresHealthProbe>();
builder.Services.AddHttpClient<IOllamaHealthProbe, OllamaHealthProbe>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(2);
});
builder.Services.AddSingleton<IDependencyHealthService, DependencyHealthService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var app = builder.Build();

app.Logger.LogInformation(
    "Starting {ServiceName} with {@RedactedStartupConfig}",
    BackendOptions.ServiceName,
    backendOptions.ToRedactedStartupConfig(dependencyOptions));

app.UseWebSockets();

app.MapGet("/", () => Results.Redirect("/health/live"));

app.MapGet("/health/live", async (
    HttpContext context,
    TimeProvider timeProvider,
    CancellationToken cancellationToken) =>
{
    await JsonResponse.WriteAsync(
        context,
        HealthResponse.Live(timeProvider.GetUtcNow()),
        StatusCodes.Status200OK,
        cancellationToken);
});

app.MapGet("/health/ready", async (
    HttpContext context,
    IDependencyHealthService healthService,
    CancellationToken cancellationToken) =>
{
    var response = await healthService.GetReadinessAsync(cancellationToken);
    await JsonResponse.WriteAsync(
        context,
        response,
        response.Status == HealthStatus.Unhealthy
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status200OK,
        cancellationToken);
});

app.MapGet("/health/dependencies", async (
    HttpContext context,
    IDependencyHealthService healthService,
    CancellationToken cancellationToken) =>
{
    var response = await healthService.GetDependencyStatusAsync(cancellationToken);
    await JsonResponse.WriteAsync(context, response, StatusCodes.Status200OK, cancellationToken);
});

app.Map("/ws/events", EventStreamEndpoint.HandleAsync);

app.Run();

public partial class Program
{
}
