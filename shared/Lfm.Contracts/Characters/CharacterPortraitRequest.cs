namespace Lfm.Contracts.Characters;

/// <summary>
/// Identifies a single WoW character for portrait URL resolution.
/// This is the shape of each element in the POST body for
/// POST /api/battlenet/character-portraits.
/// </summary>
public sealed record CharacterPortraitRequest(string Region, string Realm, string Name);
