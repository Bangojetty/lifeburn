using System.Diagnostics;
using System.Reflection.Metadata.Ecma335;
using Server.CardProperties;
using Server.Effects;

namespace Server;

public class Token : Card {
    public TokenType? tokenType;
    private static Dictionary<TokenType, Tribe> tokenToTribe = new () {
        { TokenType.Ghost, Tribe.Shadow },
        { TokenType.Goblin, Tribe.Goblin },
        { TokenType.Golem, Tribe.Golem },
        { TokenType.Herb, Tribe.Treefolk },
        { TokenType.Merfolk, Tribe.Merfolk },
        { TokenType.Plant, Tribe.Treefolk },
        { TokenType.Stone, Tribe.Golem },
        { TokenType.Treefolk, Tribe.Treefolk }
    };

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

    private static List<TokenType?> rawTokens = new() { TokenType.Stone, TokenType.Herb };

    private static List<TokenType?> summonTokens = new()
        { TokenType.Ghost, TokenType.Goblin, TokenType.Golem, TokenType.Merfolk, TokenType.Plant, TokenType.Treefolk };
    
    
    
    public Token(TokenType? tokenType, GameMatch gameMatch) : base(gameMatch.GetNextUid()) {
        // this is to go in the opposite direction as normal card ids, starting at -1 and going down from there
        id = (TokenIdsBeforeModification.IndexOf(tokenType) + 1) * -1;
        Debug.Assert(tokenType != null, "TokenType is null for the token you tried to create");
        name = tokenType.ToString();
        cost = 0;
        this.tokenType = tokenType;
        if (rawTokens.Contains(tokenType)) {
            type = CardType.Token;
        }
        if (summonTokens.Contains(tokenType)) {
            type = CardType.Summon;
        }
        tribe = tokenToTribe[(TokenType)tokenType];
    }
}