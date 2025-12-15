using Server.CardProperties;

namespace Server;

public class TokenDto {
    public TokenType tokenType;
    public string name;
    public CardType type;
    public Tribe tribe;
    public int? attack;
    public int? defense;
    public List<Keyword>? keywords;
    public List<ActivatedEffect>? activatedEffects;
    public List<PassiveEffect>? passiveEffects;
    public string? description;
}
