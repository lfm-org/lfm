// SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-FileCopyrightText: 2026 LFM contributors

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lfm.Api.Serialization;

/// <summary>
/// Newtonsoft converter that reads either a plain string or Blizzard's
/// localized-object shape (<c>{ "en_US": "Horde", "de_DE": "Horde", ... }</c>)
/// into a <see cref="string"/>.
///
/// Blizzard's WoW Game Data API emits the object shape when the caller omits
/// the <c>locale</c> query parameter. The legacy TypeScript ingestion path
/// persisted responses verbatim into Cosmos, so existing guild documents carry
/// the object shape at <c>blizzardProfileRaw.faction.name</c> and
/// <c>blizzardProfileRaw.realm.name</c>. Without this converter the Cosmos
/// SDK (Newtonsoft-backed) raised <c>JsonReaderException</c> on every read
/// and /api/guild returned 500 (see 2026-04-20 production incident).
///
/// Preference order: <c>en_US</c> → first non-empty localized value → null.
/// On write we emit the value as a plain string so new documents don't carry
/// the legacy object shape forward.
/// </summary>
internal sealed class LocalizedStringConverter : JsonConverter<string?>
{
    public override string? ReadJson(
        JsonReader reader,
        Type objectType,
        string? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        switch (reader.TokenType)
        {
            case JsonToken.Null:
                return null;
            case JsonToken.String:
                return (string?)reader.Value;
            case JsonToken.StartObject:
                var obj = JObject.Load(reader);
                var preferred = obj.Value<string?>("en_US");
                if (!string.IsNullOrEmpty(preferred)) return preferred;
                foreach (var prop in obj.Properties())
                {
                    if (prop.Value.Type == JTokenType.String)
                    {
                        var value = prop.Value.Value<string?>();
                        if (!string.IsNullOrEmpty(value)) return value;
                    }
                }
                return null;
            default:
                // Unknown token — skip the value and fall back to null so a
                // surprising shape cannot keep taking the worker down.
                reader.Skip();
                return null;
        }
    }

    public override void WriteJson(JsonWriter writer, string? value, JsonSerializer serializer)
    {
        if (value is null)
            writer.WriteNull();
        else
            writer.WriteValue(value);
    }
}
