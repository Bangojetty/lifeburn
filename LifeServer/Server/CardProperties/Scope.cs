using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Server.CardProperties;

[JsonConverter(typeof(StringEnumConverter))]
public enum Scope {
    SelfOnly,    // Only when I am the subject / only affects me
    OthersOnly,  // Only when others are the subject / only affects others (excludes me)
    All          // Any qualifying entity, including me (default behavior)
}
