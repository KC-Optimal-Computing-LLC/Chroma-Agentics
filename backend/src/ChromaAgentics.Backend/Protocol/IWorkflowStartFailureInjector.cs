namespace ChromaAgentics.Backend.Protocol;

public interface IWorkflowStartFailureInjector
{
    Task AfterWorkflowStartedPersistedAsync(CancellationToken cancellationToken);
}

public sealed class NoopWorkflowStartFailureInjector : IWorkflowStartFailureInjector
{
    public Task AfterWorkflowStartedPersistedAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
