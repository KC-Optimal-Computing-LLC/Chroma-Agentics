namespace ChromaAgentics.Backend.Contracts;

public sealed class ApprovalRequestPayload
{
    public required string ApprovalId { get; init; }
    public required string WorkflowId { get; init; }
    public required string StepId { get; init; }
    public required string ActionType { get; init; }
    public required string RiskLevel { get; init; }
    public required string Summary { get; init; }
    public string? PatchSetId { get; init; }
    public string? Command { get; init; }
    public object? ToolCall { get; init; }
    public required string RequestedByAgent { get; init; }
    public required DateTimeOffset RequestedAtUtc { get; init; }
}

public sealed class ApprovalDecisionPayload
{
    public required string ApprovalId { get; init; }
    public required string Decision { get; init; }
    public required string UserId { get; init; }
    public string? Reason { get; init; }
    public required DateTimeOffset DecidedAtUtc { get; init; }
    public object? ModifiedPatchSet { get; init; }
}
