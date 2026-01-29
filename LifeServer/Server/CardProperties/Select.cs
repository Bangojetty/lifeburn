using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Server.CardProperties;

/// <summary>
/// Represents selection configuration for effects.
/// Used for:
/// - Selecting cards from a zone (hand, graveyard, deck pool)
/// - Choosing an amount (mill up to 3, draw up to 2)
/// Unlike targeting, selection is not subject to "can't be targeted" effects.
/// </summary>
[JsonConverter(typeof(SelectConverter))]
public class Select {
    /// <summary>
    /// Zone to select from (hand, graveyard, etc.).
    /// If null, the effect type determines the selection context.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public Zone? zone;

    /// <summary>
    /// Multiple zones to select from (e.g., hand AND graveyard).
    /// Used when selection can come from multiple zones.
    /// </summary>
    public List<Zone>? zones;

    /// <summary>
    /// Minimum number to select. Defaults to max (exact) if not specified.
    /// Set to 0 explicitly for optional selection.
    /// </summary>
    public int? min;

    /// <summary>
    /// Maximum number to select. Defaults to 1 if not specified.
    /// </summary>
    public int? max;

    /// <summary>
    /// If true, player can select any amount up to all available.
    /// </summary>
    public bool upToAll;

    // Filters for selection
    [JsonConverter(typeof(StringEnumConverter))]
    public CardType? cardType;

    [JsonConverter(typeof(StringEnumConverter))]
    public Tribe? tribe;

    /// <summary>
    /// Gets the effective minimum.
    /// - If upToAll is true, defaults to 0.
    /// - If min is explicitly set, use it.
    /// - If only max is set (object format), defaults to 0 (optional).
    /// - If neither is set (simple int format), min equals max (exact).
    /// </summary>
    public int GetMin() {
        if (upToAll) return 0;
        if (min != null) return min.Value;
        // If max is set but min isn't, this is "up to N" (optional)
        if (max != null) return 0;
        // Fallback for exact selection (simple int format sets both min and max)
        return GetMax();
    }

    /// <summary>
    /// Gets the effective maximum, defaulting to 1 if not specified.
    /// </summary>
    public int GetMax() {
        // For upToAll, max will be set dynamically based on available cards
        return max ?? 1;
    }

    /// <summary>
    /// Returns true if this is an exact selection (min == max).
    /// </summary>
    public bool IsExact() => !upToAll && GetMin() == GetMax();

    /// <summary>
    /// Returns true if this is an optional selection (min == 0).
    /// </summary>
    public bool IsOptional() => upToAll || GetMin() == 0;

    /// <summary>
    /// Gets all zones to select from. Returns zones array if set, otherwise single zone in a list.
    /// </summary>
    public List<Zone> GetZones() {
        if (zones != null && zones.Count > 0) return zones;
        if (zone != null) return new List<Zone> { zone.Value };
        return new List<Zone>();
    }

    /// <summary>
    /// Returns true if selection spans multiple zones.
    /// </summary>
    public bool IsMultiZone() => zones != null && zones.Count > 1;
}

/// <summary>
/// Custom JSON converter to handle both simple int and object formats:
/// - "select": 2  ->  Select { min: 2, max: 2 }
/// - "select": { "max": 3 }  ->  Select { min: 0, max: 3 }
/// </summary>
public class SelectConverter : JsonConverter<Select> {
    public override Select? ReadJson(JsonReader reader, Type objectType, Select? existingValue, bool hasExistingValue, JsonSerializer serializer) {
        if (reader.TokenType == JsonToken.Null) {
            return null;
        }

        if (reader.TokenType == JsonToken.Integer) {
            // Simple int: exact selection
            int count = Convert.ToInt32(reader.Value);
            return new Select { min = count, max = count };
        }

        if (reader.TokenType == JsonToken.StartObject) {
            // Object format: deserialize normally
            JObject obj = JObject.Load(reader);
            Select select = new Select();

            if (obj["zone"] != null) select.zone = obj["zone"]!.ToObject<Zone>();
            if (obj["zones"] != null) select.zones = obj["zones"]!.ToObject<List<Zone>>();
            if (obj["min"] != null) select.min = obj["min"]!.ToObject<int>();
            if (obj["max"] != null) select.max = obj["max"]!.ToObject<int>();
            if (obj["upToAll"] != null) select.upToAll = obj["upToAll"]!.ToObject<bool>();
            if (obj["cardType"] != null) select.cardType = obj["cardType"]!.ToObject<CardType>();
            if (obj["tribe"] != null) select.tribe = obj["tribe"]!.ToObject<Tribe>();

            return select;
        }

        throw new JsonException($"Unexpected token type for Select: {reader.TokenType}");
    }

    public override void WriteJson(JsonWriter writer, Select? value, JsonSerializer serializer) {
        if (value == null) {
            writer.WriteNull();
            return;
        }

        // If it's a simple exact selection with no other properties, write as int
        if (value.IsExact() && value.zone == null && value.zones == null &&
            value.cardType == null && value.tribe == null && !value.upToAll) {
            writer.WriteValue(value.GetMax());
            return;
        }

        // Otherwise write as object
        serializer.Serialize(writer, new {
            zone = value.zone,
            zones = value.zones,
            min = value.min,
            max = value.max,
            upToAll = value.upToAll ? true : (bool?)null,
            cardType = value.cardType,
            tribe = value.tribe
        });
    }
}
