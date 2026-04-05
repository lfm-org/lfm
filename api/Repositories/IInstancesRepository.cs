using Lfm.Contracts.Instances;

namespace Lfm.Api.Repositories;

public interface IInstancesRepository
{
    Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct);
}
