using Lfm.Contracts.Guild;

namespace Lfm.App.Services;

public interface IGuildClient
{
    Task<GuildDto?> GetAsync(CancellationToken ct);
    Task<GuildDto?> UpdateAsync(UpdateGuildRequest request, CancellationToken ct);
}
