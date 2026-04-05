namespace Lfm.Api.Repositories;

public interface IInstancesRepository
{
    Task<IReadOnlyList<InstanceRecord>> ListAsync(CancellationToken ct);
}

public sealed record InstanceRecord(
    string Id,
    string Name,
    string ModeKey,
    string Expansion);
