using System.Diagnostics;

namespace ChromaAgentics.Backend.Observability;

public static class ProtocolActivitySource
{
    public const string Name = "ChromaAgentics.Backend.Protocol";

    public static readonly ActivitySource Instance = new(Name);
}
