namespace ChromaAgentics.Backend.Contracts;

public sealed class ErrorPayload
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? Detail { get; init; }
}
