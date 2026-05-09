using System.Text.Json;

namespace ChromaAgentics.Backend.Health;

public static class JsonResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync<T>(
        HttpContext context,
        T value,
        int statusCode,
        CancellationToken cancellationToken = default)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var json = JsonSerializer.Serialize(value, JsonOptions);
        await context.Response.WriteAsync(json, cancellationToken);
    }
}
