using System.Diagnostics;
using Newtonsoft.Json;
using Server.CardProperties;

namespace Server;

public class Token : Card {
    public TokenType? tokenType;

    // Cache for loaded token definitions
    private static Dictionary<TokenType, TokenDto> tokenCache = new();

    // For generating unique negative IDs for tokens
    private static List<TokenType?> TokenIdsBeforeModification = new() {
        TokenType.Ghost,
        TokenType.Goblin,
        TokenType.Golem,
        TokenType.Merfolk,
        TokenType.Plant,
        TokenType.Treefolk,
        TokenType.Stone,
        TokenType.Herb
    };

    /// <summary>
    /// Loads a TokenDto from JSON, using cache if available
    /// </summary>
    private static TokenDto GetTokenDto(TokenType tokenType) {
        if (tokenCache.TryGetValue(tokenType, out TokenDto? cached)) {
            return cached;
        }

        string tokenJson = Utils.GetTokenJson(tokenType);
        JsonSerializerSettings settings = new();
        settings.Converters.Add(new EffectTypeConverter());
        TokenDto tokenDto = JsonConvert.DeserializeObject<TokenDto>(tokenJson, settings)!;
        tokenCache[tokenType] = tokenDto;
        return tokenDto;
    }

    public Token(TokenType? tokenType, GameMatch gameMatch) : base(gameMatch.GetNextUid()) {
        Debug.Assert(tokenType != null, "TokenType is null for the token you tried to create");

        // Load base token definition from JSON
        TokenDto tokenDto = GetTokenDto((TokenType)tokenType);

        // Set properties from JSON
        id = (TokenIdsBeforeModification.IndexOf(tokenType) + 1) * -1;
        currentGameMatch = gameMatch;
        this.tokenType = tokenType;
        name = tokenDto.name;
        cost = 0;
        type = tokenDto.type;
        tribe = tokenDto.tribe;
        attack = tokenDto.attack;
        defense = tokenDto.defense;
        keywords = tokenDto.keywords;
        description = tokenDto.description;

        // Deep copy activated effects so each token instance has its own
        if (tokenDto.activatedEffects != null) {
            activatedEffects = tokenDto.activatedEffects.Select(ae => ae.Clone()).ToList();
            // Set sourceCard on each activated effect and its nested effects
            foreach (var ae in activatedEffects) {
                ae.sourceCard = this;
                if (ae.effects != null) {
                    foreach (var e in ae.effects) {
                        e.sourceCard = this;
                    }
                }
            }
        }

        // Deep copy passive effects
        if (tokenDto.passiveEffects != null) {
            passiveEffects = tokenDto.passiveEffects.ToList();
        }
    }
}