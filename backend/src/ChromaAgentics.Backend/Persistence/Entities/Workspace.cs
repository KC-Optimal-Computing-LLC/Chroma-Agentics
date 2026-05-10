namespace ChromaAgentics.Backend.Persistence.Entities;

public sealed class Workspace
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
