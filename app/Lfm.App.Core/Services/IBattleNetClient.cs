using Lfm.Contracts.Characters;

namespace Lfm.App.Services;

public interface IBattleNetClient
{
    Task<IReadOnlyList<CharacterDto>?> GetCharactersAsync(CancellationToken ct);

    Task<IReadOnlyList<CharacterDto>?> RefreshCharactersAsync(CancellationToken ct);

    Task<IDictionary<string, string>?> GetPortraitsAsync(
        IEnumerable<CharacterPortraitRequest> requests,
        CancellationToken ct);
}
