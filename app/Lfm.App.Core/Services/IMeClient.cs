using Lfm.Contracts.Me;

namespace Lfm.App.Services;

public interface IMeClient
{
    Task<MeResponse?> GetAsync(CancellationToken ct);

    Task<UpdateMeResponse?> UpdateAsync(UpdateMeRequest request, CancellationToken ct);

    Task<bool> DeleteAsync(CancellationToken ct);
}
