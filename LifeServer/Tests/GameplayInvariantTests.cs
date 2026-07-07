using Server;
using Server.CardProperties;

namespace Tests;

/// <summary>
/// Tests for the known engine bug classes from the Dec 2025 sessions:
/// zone-transition bookkeeping, trigger scoping defaults, and isCost payment/fizzle.
/// </summary>
[TestFixture]
public class GameplayInvariantTests {

    private static GameMatch NewMatch(out Player p1, out Player p2, int matchId = 1, int playerIdBase = 1) {
        p1 = new Player("TestPlayerOne" + matchId, playerIdBase);
        p2 = new Player("TestPlayerTwo" + matchId, playerIdBase + 1);
        // 40-card decks of simple vanilla summons (6 = Golem, 7 = RockGolem)
        p1.deck = Enumerable.Range(0, 40).Select(i => Card.GetCard(100 + i, 6)).ToList();
        p2.deck = Enumerable.Range(0, 40).Select(i => Card.GetCard(200 + i, 7)).ToList();
        GameMatch match = new GameMatch(matchId, p1, p2);
        foreach (Card c in p1.deck) c.currentGameMatch = match;
        foreach (Card c in p2.deck) c.currentGameMatch = match;
        match.InitializeMatch();
        return match;
    }

    [Test]
    public void CreatingStones_DoesNotZeroHandCardCosts() {
        GameMatch match = NewMatch(out Player p1, out _);
        // Realistic golem hand: EarthquakeGolem, FoundryGolem, Quarry, DigitalStone, RockToss
        int[] ids = { 21, 26, 29, 36, 23 };
        List<Card> hand = ids.Select((id, i) => Card.GetCard(600 + i, id, match)).ToList();
        foreach (Card c in hand) {
            c.currentZone = Zone.Hand;
            c.playerHandOf = p1;
            p1.hand.Add(c);
            p1.ownedCards.Add(c);
        }

        // Baseline right after match init (bot test dummies already spawned)
        foreach (Card c in hand) {
            Assert.That(new CardDisplayData(c).cost, Is.EqualTo(c.cost), $"{c.name} at match start");
        }

        // Quarry-style stone creation, then the passive sweep that follows every event
        for (int i = 0; i < 4; i++) p1.tokens.Add(new Token(TokenType.Stone, match));
        match.CheckForPassives();

        foreach (Card c in hand) {
            int expected = c.id == 26 ? 0 : c.cost;  // FoundryGolem legitimately 0 at 4+ stones
            Assert.That(new CardDisplayData(c).cost, Is.EqualTo(expected), $"{c.name} after 4 stones");
        }
    }

    [Test]
    public void StatConditionOnSelf_DoesNotInfiniteRecurse() {
        // Merfolk Elite (251): "While either player controls a 1/1 summon, Merfolk Elite
        // has Spectral." Its OneOneInPlay condition reads every in-play card's attack -
        // including its own, whose computation re-checks the condition. Before the
        // re-entrancy guard this overflowed the stack and killed the process.
        GameMatch match = NewMatch(out Player p1, out _);
        Card elite = Card.GetCard(700, 251, match);
        elite.currentZone = Zone.Play;
        p1.playField.Add(elite);
        // A real 1/1 in play so the condition is actually satisfied (Golem token is 1/1)
        p1.tokens.Add(new Token(TokenType.Golem, match));

        // None of these may recurse; a failure would be a StackOverflow crashing the runner
        Assert.That(elite.GetAttack(), Is.EqualTo(3));
        Assert.That(elite.GetDefense(), Is.EqualTo(3));
        Assert.That(elite.GetKeywords(), Does.Contain(Keyword.Spectral),
            "1/1 in play should grant Merfolk Elite Spectral");

        // And with no 1/1 present, no Spectral - and still no recursion
        p1.tokens.Clear();
        Assert.That(elite.GetKeywords(), Does.Not.Contain(Keyword.Spectral));
    }

    [Test]
    public void ConcurrentMatches_AreIndependent() {
        GameMatch matchA = NewMatch(out Player a1, out _, matchId: 1, playerIdBase: 1);
        GameMatch matchB = NewMatch(out Player b1, out _, matchId: 2, playerIdBase: 3);

        Matches registry = new Matches();
        registry.SetMatchData(matchA);
        registry.SetMatchData(matchB);

        // Mutating match A must not touch match B
        int bHandBefore = b1.hand.Count;
        int bLifeBefore = b1.lifeTotal;
        Card aCard = a1.hand.First();
        matchA.SendToZone(a1, Zone.Graveyard, aCard);
        matchA.GainLife(a1, 5);

        Assert.That(b1.hand.Count, Is.EqualTo(bHandBefore));
        Assert.That(b1.lifeTotal, Is.EqualTo(bLifeBefore));
        Assert.That(registry.GetMatchData(1), Is.SameAs(matchA));
        Assert.That(registry.GetMatchData(2), Is.SameAs(matchB));

        // Ending match A leaves match B fully registered and playable
        registry.EndMatch(1);
        Assert.That(registry.GetMatchData(1), Is.Null);
        Assert.That(registry.GetMatchData(2), Is.SameAs(matchB));
        Assert.That(registry.GetPlayerMatchId(b1.playerId), Is.EqualTo(2));
    }

    [Test]
    public void FinishedMatches_StayRegisteredDuringGracePeriod() {
        GameMatch match = NewMatch(out Player p1, out _);
        Matches registry = new Matches();
        registry.SetMatchData(match);

        Assert.That(registry.GetFinishedMatches(0), Is.Empty, "Match not over yet");

        match.EndGame(p1, "test");
        Assert.That(registry.GetFinishedMatches(60), Is.Empty,
            "Freshly finished match must survive the grace period so clients can fetch game-over events");
        Assert.That(registry.GetFinishedMatches(0), Is.EquivalentTo(new[] { match.matchId }),
            "Once grace expires the match is eligible for cleanup");
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
