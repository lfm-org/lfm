using Lfm.Contracts.Characters;

namespace Lfm.Contracts.Raiders;

public sealed record AddCharacterResponse(string SelectedCharacterId, CharacterDto Character);
