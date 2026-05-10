namespace ChromaAgentics.Backend.Protocol;

public sealed record ProtocolValidationResult(
    bool IsValid,
    string? ErrorCode = null,
    string? ErrorMessage = null)
{
    public static ProtocolValidationResult Valid { get; } = new(true);

    public static ProtocolValidationResult Error(string code, string message)
    {
        return new ProtocolValidationResult(false, code, message);
    }
}
