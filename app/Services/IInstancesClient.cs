using Lfm.Contracts.Instances;

namespace Lfm.App.Services;

public interface IInstancesClient
{
    Task<IReadOnlyList<InstanceDto>> ListAsync(CancellationToken ct);
}
