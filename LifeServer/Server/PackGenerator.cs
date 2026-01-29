using System;
using System.Collections.Generic;
using System.Linq;
using Server.CardProperties;

namespace Server;

public static class PackGenerator {
    private const int CardsPerPack = 15;
    private const int TotalCardAmount = 281;
    private const double RareChance = 0.30;  // 30% chance pack contains a rare
    private static readonly Random Random = new();

    /// <summary>
    /// Generates draft packs for a 2-player draft.
    /// Creates 8 packs total (4 per player), each with 15 cards.
    /// Pack composition: 1 guaranteed card from each tribe (5 cards), 10 random cards.
    /// 30% chance the pack contains a rare card.
    /// </summary>
    public static List<DraftPack> GeneratePacks(int packCount = 8) {
        // Load all cards and group by tribe and rarity
        var allCards = LoadAllCards();
        var cardsByTribe = GroupCardsByTribe(allCards);
        var rareCards = allCards.Where(c => c.rarity == Rarity.Rare).Select(c => c.id).ToList();
        var nonRareCards = allCards.Where(c => c.rarity != Rarity.Rare).Select(c => c.id).ToList();

        List<DraftPack> packs = new();

        for (int i = 0; i < packCount; i++) {
            DraftPack pack = new DraftPack {
                packIndex = i % 4,  // 0-3 for each player's packs
                cardIds = new List<int>(),
                isOpened = false
            };

            // Add 1 guaranteed card from each tribe (5 cards total)
            foreach (Tribe tribe in Enum.GetValues(typeof(Tribe))) {
                var tribeCards = cardsByTribe[tribe];
                if (tribeCards.Count > 0) {
                    int randomCard = tribeCards[Random.Next(tribeCards.Count)];
                    pack.cardIds.Add(randomCard);
                }
            }

            // Determine if this pack has a rare (30% chance)
            bool hasRare = Random.NextDouble() < RareChance;

            // Fill remaining slots (10 cards)
            int remainingSlots = CardsPerPack - pack.cardIds.Count;

            if (hasRare && rareCards.Count > 0) {
                // Add one rare
                int rareCard = rareCards[Random.Next(rareCards.Count)];
                pack.cardIds.Add(rareCard);
                remainingSlots--;
            }

            // Fill rest with non-rare cards
            for (int j = 0; j < remainingSlots; j++) {
                if (nonRareCards.Count > 0) {
                    int randomCard = nonRareCards[Random.Next(nonRareCards.Count)];
                    pack.cardIds.Add(randomCard);
                }
            }

            // Shuffle the pack so tribe cards aren't always first
            pack.cardIds = pack.cardIds.OrderBy(_ => Random.Next()).ToList();

            packs.Add(pack);
        }

        return packs;
    }

    /// <summary>
    /// Assigns packs to players. First 4 packs go to player 1, next 4 to player 2.
    /// </summary>
    public static void AssignPacksToPlayers(List<DraftPack> packs, DraftPlayerState player1, DraftPlayerState player2) {
        for (int i = 0; i < 4 && i < packs.Count; i++) {
            packs[i].packIndex = i;
            player1.packs.Add(packs[i]);
        }

        for (int i = 4; i < 8 && i < packs.Count; i++) {
            packs[i].packIndex = i - 4;
            player2.packs.Add(packs[i]);
        }
    }

    private static List<Card> LoadAllCards() {
        List<Card> cards = new();
        for (int i = 0; i < TotalCardAmount; i++) {
            try {
                Card card = Card.GetCardSimple(i);
                if (card != null && card.type != CardType.Token) {
                    cards.Add(card);
                }
            } catch {
                // Skip cards that fail to load
            }
        }
        return cards;
    }

    private static Dictionary<Tribe, List<int>> GroupCardsByTribe(List<Card> cards) {
        var grouped = new Dictionary<Tribe, List<int>>();

        foreach (Tribe tribe in Enum.GetValues(typeof(Tribe))) {
            grouped[tribe] = new List<int>();
        }

        foreach (var card in cards) {
            if (grouped.ContainsKey(card.tribe)) {
                grouped[card.tribe].Add(card.id);
            }
        }

        return grouped;
    }
}
