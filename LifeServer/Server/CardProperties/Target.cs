using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

/// <summary>
/// Represents targeting configuration for effects that target in-play objects.
/// Targeting is subject to game mechanics like hexproof, "can't be targeted", etc.
/// </summary>
public class Target {
    [JsonConverter(typeof(StringEnumConverter))]
    public TargetType type;

    /// <summary>
    /// Minimum number of targets required. Defaults to max if not specified (exact targeting).
    /// </summary>
    public int? min;

    /// <summary>
    /// Maximum number of targets allowed. Defaults to 1 if not specified.
    /// </summary>
    public int? max;

    /// <summary>
    /// Restrict targets to "self" (your summons) or "opponent" (opponent's summons).
    /// If null, targets can be from either player.
    /// </summary>
    public string? sourcePlayer;

    /// <summary>
    /// If true, requires at least 1 target from each player.
    /// Used for effects like "target summon you control and target summon opponent controls".
    /// </summary>
    public bool requireOneFromEach;

    /// <summary>
    /// Gets the effective minimum targets, defaulting to max (exact) if not specified.
    /// </summary>
    public int GetMin() => min ?? GetMax();

    /// <summary>
    /// Gets the effective maximum targets, defaulting to 1 if not specified.
    /// </summary>
    public int GetMax() => max ?? 1;
}
