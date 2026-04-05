namespace Lfm.App.Services;

public interface IInstancesClient
{
    Task<IReadOnlyList<ClientInstanceRecord>> ListAsync(CancellationToken ct);
}

// Temporary — replaced by shared Contracts.InstanceDto in Task A8.3
public sealed record ClientInstanceRecord(string Id, string Name, string ModeKey, string Expansion);
