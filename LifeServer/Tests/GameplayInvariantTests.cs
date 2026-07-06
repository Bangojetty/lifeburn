using Server;
using Server.CardProperties;

namespace Tests;

/// <summary>
/// Tests for the known engine bug classes from the Dec 2025 sessions:
/// zone-transition bookkeeping, trigger scoping defaults, and isCost payment/fizzle.
/// </summary>
[TestFixture]
public class GameplayInvariantTests {

    private static GameMatch NewMatch(out Player p1, out Player p2) {
        p1 = new Player("TestPlayerOne", 1);
        p2 = new Player("TestPlayerTwo", 2);
        // 40-card decks of simple vanilla summons (6 = Golem, 7 = RockGolem)
        p1.deck = Enumerable.Range(0, 40).Select(i => Card.GetCard(100 + i, 6)).ToList();
        p2.deck = Enumerable.Range(0, 40).Select(i => Card.GetCard(200 + i, 7)).ToList();
        GameMatch match = new GameMatch(1, p1, p2);
        foreach (Card c in p1.deck) c.currentGameMatch = match;
        foreach (Card c in p2.deck) c.currentGameMatch = match;
        match.InitializeMatch();
        return match;
    }

    /// <summary>
    /// Counts how many zone lists a card appears in across both players.
    /// A card must never be in two zone lists at once (double-trigger bug class).
    /// </summary>
    private static int ZoneMembershipCount(GameMatch match, Card card) {
        int count = 0;
        foreach (Player p in new[] { match.playerOne, match.playerTwo }) {
            if (p.deck != null && p.deck.Contains(card)) count++;
            if (p.hand.Contains(card)) count++;
            if (p.playField.Contains(card)) count++;
            if (p.graveyard.Contains(card)) count++;
            if (p.exile.Contains(card)) count++;
        }
        return count;
    }

    [Test]
    public void ZoneTransitions_CardIsNeverInTwoZonesAtOnce() {
        GameMatch match = NewMatch(out Player p1, out _);
        Card card = p1.hand.First();

        foreach (Zone destination in new[] { Zone.Play, Zone.Graveyard, Zone.Exile, Zone.Hand, Zone.Graveyard, Zone.Deck }) {
            DeckDestination? deckDestination = destination == Zone.Deck
                ? new DeckDestination(DeckDestinationType.Top, false, false)
                : null;
            match.SendToZone(p1, destination, card, deckDestination);
            Assert.That(ZoneMembershipCount(match, card), Is.EqualTo(1),
                $"After moving to {destination}, card should be in exactly one zone list");
        }
    }

    [Test]
    public void ZoneTransitions_AfterInitialize_AllCardsInExactlyOneZone() {
        GameMatch match = NewMatch(out Player p1, out Player p2);
        foreach (Player p in new[] { p1, p2 }) {
            foreach (Card c in p.ownedCards) {
                Assert.That(ZoneMembershipCount(match, c), Is.EqualTo(1), $"{c.name} (uid {c.uid})");
            }
        }
    }

    [Test]
    public void TriggerScoping_TriggeredEffectDefaultsToSelfOnly() {
        // "when this enters play" triggers with no scope must NOT fire for every card
        var te = Newtonsoft.Json.JsonConvert.DeserializeObject<TriggeredEffect>("""{ "trigger": "enteredZone", "zone": "play" }""");
        Assert.That(te!.scope, Is.EqualTo(Scope.SelfOnly));
    }

    [Test]
    public void TriggerScoping_PassiveAndEffectDefaultToAll() {
        var pe = Newtonsoft.Json.JsonConvert.DeserializeObject<PassiveEffect>("""{ "passive": "grantKeyword" }""");
        Assert.That(pe!.scope, Is.EqualTo(Scope.All));
        Effect effect = new Effect(EffectType.GainLife);
        Assert.That(effect.scope, Is.EqualTo(Scope.All));
    }

    [Test]
    public void IsCost_SacrificeSelf_OnlyPayableWhileInPlay() {
        GameMatch match = NewMatch(out Player p1, out _);
        Card source = p1.hand.First();
        Effect cost = new Effect(EffectType.Sacrifice) { isCost = true, scope = Scope.SelfOnly, sourceCard = source };

        source.currentZone = Zone.Hand;
        Assert.That(cost.CanPayCost(match, p1), Is.False, "Cannot sacrifice a card that is not in play");

        match.SendToZone(p1, Zone.Play, source);
        source.currentZone = Zone.Play;
        Assert.That(cost.CanPayCost(match, p1), Is.True, "Can sacrifice a card in play");
        Assert.That(cost.NeedsCostSelection(match, p1), Is.False, "Sacrifice self is auto-paid");
    }

    [Test]
    public void IsCost_RevealSelf_AutoPaid() {
        GameMatch match = NewMatch(out Player p1, out _);
        Effect cost = new Effect(EffectType.Reveal) { isCost = true, scope = Scope.SelfOnly, sourceCard = p1.hand.First() };
        Assert.That(cost.CanPayCost(match, p1), Is.True);
        Assert.That(cost.NeedsCostSelection(match, p1), Is.False);
    }

    [Test]
    public void IsCost_TokenSacrifice_SelectionOnlyWhenMultipleCandidates() {
        GameMatch match = NewMatch(out Player p1, out _);
        Effect cost = new Effect(EffectType.Sacrifice) { isCost = true, tokenType = TokenType.Stone };

        Assert.That(cost.CanPayCost(match, p1), Is.False, "No stones - cost unpayable");

        p1.tokens.Add(new Token(TokenType.Stone, match));
        Assert.That(cost.CanPayCost(match, p1), Is.True, "One stone - payable");
        Assert.That(cost.NeedsCostSelection(match, p1), Is.False, "One stone - auto-paid");

        p1.tokens.Add(new Token(TokenType.Stone, match));
        Assert.That(cost.NeedsCostSelection(match, p1), Is.True, "Two stones - player must choose");
        Assert.That(cost.GetCostSelectableUids(match, p1), Has.Count.EqualTo(2));
    }

    [Test]
    public void ModifyCostPassive_OnlyAffectsItsOwnCard_AndTracksConditionLive() {
        GameMatch match = NewMatch(out Player p1, out _);
        Card foundry = Card.GetCard(500, 26, match);   // Foundry Golem: "costs 0 if you control 4+ stones"
        Card other = Card.GetCard(501, 21, match);     // Earthquake Golem, cost 1
        foundry.currentZone = Zone.Hand;
        other.currentZone = Zone.Hand;
        foundry.playerHandOf = p1;
        other.playerHandOf = p1;
        p1.hand.Add(foundry);
        p1.hand.Add(other);
        p1.ownedCards.Add(foundry);
        p1.ownedCards.Add(other);
        int otherBaseCost = other.cost;

        // With 4 stones: the aura pass must NOT leak Foundry Golem's cost-0 onto other cards
        for (int i = 0; i < 4; i++) p1.tokens.Add(new Token(TokenType.Stone, match));
        match.CheckForPassives();
        Assert.That(other.grantedPassives, Is.Empty, "Foundry Golem's modifyCost must not spread to other cards");
        Assert.That(other.GetCost(), Is.EqualTo(otherBaseCost));
        Assert.That(foundry.GetCost(), Is.EqualTo(0), "Foundry Golem itself costs 0 with 4 stones");

        // Stones go away: cost must recover immediately (condition is evaluated live)
        p1.tokens.Clear();
        Assert.That(foundry.GetCost(), Is.EqualTo(4), "Foundry Golem cost must recover when stones leave");
        Assert.That(other.GetCost(), Is.EqualTo(otherBaseCost));
    }

    [Test]
    public void IsCost_UnpayableCost_FizzlesRemainingEffects_ButKeepsEarlierOnes() {
        GameMatch match = NewMatch(out Player p1, out _);
        Card source = p1.hand.First();
        int lifeBefore = p1.lifeTotal;

        Effect gainBefore = Effect.CreateEffect(new Effect(EffectType.GainLife) { amount = 2 }, source);
        // Sacrificing a stone is impossible (no stones) -> everything after must fizzle
        Effect unpayable = Effect.CreateEffect(new Effect(EffectType.Sacrifice) { isCost = true, tokenType = TokenType.Stone }, source);
        Effect gainAfter = Effect.CreateEffect(new Effect(EffectType.GainLife) { amount = 5 }, source);

        StackObj stackObj = new StackObj(source, StackObjType.TriggeredEffect,
            new List<Effect> { gainBefore, unpayable, gainAfter }, Zone.Play, p1);
        stackObj.ResolveStackObj(match);

        Assert.That(p1.lifeTotal, Is.EqualTo(lifeBefore + 2),
            "Effects before the unpayable cost resolve; the cost and everything after fizzle");
    }
}
