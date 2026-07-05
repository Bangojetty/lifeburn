using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Authorization;
using Newtonsoft.Json;
using Server.CardProperties;

namespace Server;

public class Utils {

    private static string? _dataBasePath;

    private static string GetDataBasePath() {
        if (_dataBasePath != null) return _dataBasePath;

        // Look for Data folder in the same directory as the executable
        string exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".";
        string candidate = Path.Combine(exeDir, "Data", "Cards");
        if (Directory.Exists(candidate)) {
            _dataBasePath = Path.Combine(exeDir, "Data");
            Console.WriteLine($"[Utils] Found Data folder at: {_dataBasePath}");
            return _dataBasePath;
        }

        // Fallback: search upward from assembly location
        string? dir = AppContext.BaseDirectory;
        while (dir != null) {
            candidate = Path.Combine(dir, "Data", "Cards");
            if (Directory.Exists(candidate)) {
                _dataBasePath = Path.Combine(dir, "Data");
                Console.WriteLine($"[Utils] Found Data folder at: {_dataBasePath}");
                return _dataBasePath;
            }
            dir = Path.GetDirectoryName(dir);
        }

        throw new DirectoryNotFoundException($"Could not find Data folder. Checked: {exeDir}");
    }

    public static string GetCardJson(int cardId) {
        string result = cardId.ToString("D3");
        string directoryPath = Path.Combine(GetDataBasePath(), "Cards");
        string[] matchingFiles = Directory.GetFiles(directoryPath, result + "*");
        using (StreamReader streamReader = new StreamReader(matchingFiles[0], Encoding.UTF8))
        {
            return streamReader.ReadToEnd().Replace("\r\n", Environment.NewLine);
        }
    }

    public static string GetTokenJson(TokenType tokenType) {
        string directoryPath = Path.Combine(GetDataBasePath(), "Tokens");
        string fileName = tokenType.ToString() + ".json";
        string filePath = Path.Combine(directoryPath, fileName);
        using (StreamReader streamReader = new StreamReader(filePath, Encoding.UTF8))
        {
            return streamReader.ReadToEnd().Replace("\r\n", Environment.NewLine);
        }
    }

    /// <summary>
    /// Strictly deserializes every card JSON in Data/Cards at startup. Any property name
    /// that has no matching C# member (a typo like "statModfiers") fails loudly instead
    /// of being silently ignored. Throws if any card is invalid.
    /// </summary>
    public static void ValidateAllCards() {
        string cardsDir = Path.Combine(GetDataBasePath(), "Cards");
        JsonSerializerSettings strict = new() {
            MissingMemberHandling = MissingMemberHandling.Error,
        };
        strict.Converters.Add(new EffectTypeConverter());

        List<string> failures = new();
        string[] files = Directory.GetFiles(cardsDir, "*.json");
        foreach (string path in files) {
            try {
                JsonConvert.DeserializeObject<CardDto>(File.ReadAllText(path), strict);
            } catch (Exception ex) {
                failures.Add($"{Path.GetFileName(path)}: {ex.Message}");
            }
        }
        if (failures.Count > 0) {
            throw new InvalidDataException(
                $"[CardValidation] {failures.Count} of {files.Length} card files failed validation:\n  " +
                string.Join("\n  ", failures));
        }
        Console.WriteLine($"[CardValidation] {files.Length} card files validated OK");
    }

    public static bool CheckPlayability(Card card, GameMatch gameMatch, Player player, bool fromTopOfDeck = false) {
        // For tokens, check if they have any activatable abilities (granted or innate)
        if (card.type == CardType.Token) {
            foreach (ActivatedEffect aEffect in card.GetActivatedEffects()) {
                if (aEffect.CostIsAvailable(gameMatch, player)) return true;
            }
            return false;
        }

        // Check if card is castable from top of deck (AdditionalSummonTopCard passive)
        bool isFromDeck = fromTopOfDeck && player.deck != null && player.deck.Count > 0 && player.deck.First() == card;

        if (!player.hand.Contains(card) && !isFromDeck) {
            bool isInGraveyard = player.graveyard.Contains(card);
            foreach (ActivatedEffect aEffect in card.GetActivatedEffects()) {
                // Skip hand-only abilities when card is not in hand (e.g., Tree Giant discard ability)
                if (aEffect.activateFromHand) continue;
                // Check activated effect conditions (e.g., inZone: graveyard)
                if (aEffect.conditions != null) {
                    bool conditionsMet = aEffect.conditions.All(c => c.Verify(gameMatch, player, null, card));
                    if (!conditionsMet) continue;
                } else if (isInGraveyard) {
                    // Cards in graveyard require explicit inZone: graveyard condition to be activatable
                    // (e.g., Ghostly Looter has this, Dark Shade doesn't)
                    continue;
                }
                // Check summon timing restrictions (must be your turn, main phase, empty stack, within summon limit)
                if (aEffect.summonTiming) {
                    if (gameMatch.turnPlayerId != player.playerId) continue;
                    if (gameMatch.currentPhase is not (Phase.Main or Phase.SecondMain)) continue;
                    if (gameMatch.stack.Count > 0) continue;
                    int summonLimit = 1 + player.turnSummonLimitBonus;
                    if (player.turnSummonCount >= summonLimit) continue;
                }
                if (!aEffect.CostIsAvailable(gameMatch, player)) continue;
                return true;
            }
            return false;
        }
        
        // PASSIVE-CHECK: check for BypassSummonLimit passive
        bool hasBypassSummonLimit = false;
        if (card.passiveEffects != null) {
            foreach (PassiveEffect passive in card.passiveEffects) {
                if (passive.passive != Passive.BypassSummonLimit) continue;
                // verify any conditions for the passive
                if (passive.conditions == null) {
                    hasBypassSummonLimit = true;
                    continue;
                }

                foreach (Condition condition in passive.conditions) {
                    if (!condition.Verify(gameMatch, player)) {
                        hasBypassSummonLimit = false;
                        break;
                    }

                    hasBypassSummonLimit = true;
                }
            }
        }
        
        // check a
        

        // CARD TYPE CONDTIONS:
        switch (card.type) {
            case CardType.Spell:
                // Finale spells can always be cast (cost capped at lifeTotal - 1)
                bool hasFinale = card.HasKeyword(Keyword.Finale);
                // must have enough life OR be able to pay an alternate cost OR have Finale
                bool spellCanPayLife = player.lifeTotal > card.GetCost();
                bool spellCanPayAltCost = CanPaySacrificeAlternateCost(player, card) ||
                                          CanPayExileFromHandAlternateCost(player, card);
                if (!spellCanPayLife && !spellCanPayAltCost && !hasFinale) return false;
                // exhaust spells can only be cast on your turn
                if (card.HasKeyword(Keyword.Exhaust) && gameMatch.turnPlayerId != player.playerId) return false;
                break;
            case CardType.Object:
                // must have enough life OR be able to pay an alternate cost
                bool objectCanPayLife = player.lifeTotal > card.GetCost();
                bool objectCanPayAltCost = CanPaySacrificeAlternateCost(player, card) ||
                                           CanPayExileFromHandAlternateCost(player, card);
                if (!objectCanPayLife && !objectCanPayAltCost) return false;
                // must be your turn
                if (gameMatch.turnPlayerId != player.playerId) return false;
                // must be main phase
                if (gameMatch.currentPhase is not (Phase.Main or Phase.SecondMain)) return false;
                // stack must be empty
                if (gameMatch.stack.Count > 0) return false;
                break;
            case CardType.Summon:
                // Check if card has special cast restrictions that override default summon rules
                bool hasOnOpponentAttack = card.castRestrictions?.Contains(CastRestriction.OnOpponentAttack) ?? false;

                // must have enough tribute value OR have a payable alternate cost
                bool canPayTribute = card.GetCost() <= GetTributeValue(player, card);
                bool canPayAlternateCost = CanPaySacrificeAlternateCost(player, card) ||
                                           CanPayExileFromHandAlternateCost(player, card);
                if (!canPayTribute && !canPayAlternateCost) {
                    return false;
                }

                if (hasOnOpponentAttack) {
                    // OnOpponentAttack summons bypass normal turn/phase/stack restrictions
                    // The cast restriction itself will enforce the correct timing
                } else {
                    // Normal summon restrictions
                    // must be your turn
                    if (gameMatch.turnPlayerId != player.playerId) return false;
                    // must be a main phase
                    if (gameMatch.currentPhase is not (Phase.Main or Phase.SecondMain)) return false;
                    // stack must be empty
                    if (gameMatch.stack.Count > 0) return false;
                    // must be your first summon this turn unless the card has the ByPassSummonLimit passive
                    int summonLimit = 1 + player.turnSummonLimitBonus;
                    if (player.turnSummonCount >= summonLimit && !hasBypassSummonLimit) return false;
                }
                break;
        }
        
        // cards with required targets must have targets available
        if (card.stackEffects != null) {
            foreach (Effect sEffect in card.stackEffects) {
                // For choose effects, at least one choice must have valid targets
                if (sEffect.effect == EffectType.Choose && sEffect.choices != null) {
                    bool anyChoiceHasTargets = false;
                    foreach (List<Effect> choiceEffects in sEffect.choices) {
                        bool choiceHasValidTargets = true;
                        foreach (Effect choiceEffect in choiceEffects) {
                            // Skip resolveTarget effects - they select targets at resolution, not cast time
                            // Skip targetBasedOn effects - they have auto-determined targets
                            if (choiceEffect.HasTargeting() && !choiceEffect.resolveTarget && choiceEffect.targetBasedOn == null && gameMatch.GetPossibleTargets(player, choiceEffect).Count == 0) {
                                choiceHasValidTargets = false;
                                break;
                            }
                        }
                        if (choiceHasValidTargets) {
                            anyChoiceHasTargets = true;
                            break;
                        }
                    }
                    if (!anyChoiceHasTargets) return false;
                }
                // For non-choose effects, check targets directly
                // Skip resolveTarget effects - they select targets at resolution, not cast time
                // Skip targetBasedOn effects - they have auto-determined targets
                else if (sEffect.HasTargeting() && !sEffect.resolveTarget && sEffect.targetBasedOn == null && gameMatch.GetPossibleTargets(player, sEffect).Count == 0) return false;
            }
        }
        // all additional costs must be available
        if (card.additionalCosts != null) {
            if (card.additionalCosts.Any(aCost => !aCost.CostIsAvailable(gameMatch, player))) return false;
        }

        // check cast restrictions
        if (card.castRestrictions != null) {
            foreach (CastRestriction restriction in card.castRestrictions) {
                switch (restriction) {
                    case CastRestriction.OnOpponentAttack:
                        // Can only cast during opponent's combat phase (attackers not required)
                        if (gameMatch.turnPlayerId == player.playerId) return false;
                        if (gameMatch.currentPhase != Phase.Combat) return false;
                        break;
                    case CastRestriction.InResponse:
                        // Can only cast when the stack is not empty
                        if (gameMatch.stack.Count == 0) return false;
                        break;
                    case CastRestriction.OpponentsTurn:
                        // Can only cast on opponent's turn
                        if (gameMatch.turnPlayerId == player.playerId) return false;
                        break;
                    case CastRestriction.ControlGoblin:
                        // Must control at least one goblin
                        bool hasGoblin = player.playField.Any(c => c.tribe == Tribe.Goblin) ||
                                        player.tokens.Any(t => t.tribe == Tribe.Goblin);
                        if (!hasGoblin) return false;
                        break;
                    case CastRestriction.OpponentsTurnBeforeCombat:
                        // Must be opponent's turn AND before combat phase (Draw or Main)
                        if (gameMatch.turnPlayerId == player.playerId) return false;
                        if (gameMatch.currentPhase != Phase.Draw && gameMatch.currentPhase != Phase.Main) return false;
                        break;
                    case CastRestriction.DuringCombat:
                        // Must be in combat phase with attackers assigned
                        if (gameMatch.currentPhase != Phase.Combat) return false;
                        if (gameMatch.currentAttackUids.Count == 0) return false;
                        break;
                    case CastRestriction.BothPlayersHaveSummons:
                        // Both players must have at least one summon in play
                        Player opponent = gameMatch.GetOpponent(player);
                        bool playerHasSummon = player.playField.Any(c => c.type == CardType.Summon) || player.tokens.Any(t => t.type == CardType.Summon);
                        bool opponentHasSummon = opponent.playField.Any(c => c.type == CardType.Summon) || opponent.tokens.Any(t => t.type == CardType.Summon);
                        if (!playerHasSummon || !opponentHasSummon) return false;
                        break;
                }
            }
        }

        // all conditions are met
        return true;
    }

    /// <summary>
    /// Calculates the total tribute value for a player, accounting for tribute multipliers.
    /// </summary>
    public static int GetTributeValue(Player player, Card cardRequiringTribute) {
        // If player has CantTribute passive, they can't tribute (return 0)
        if (player.playerPassives.Any(p => p.passive == Passive.CantTribute)) {
            return 0;
        }

        int tributeValue = 0;

        // Each summon on the field counts as 1 tribute
        tributeValue += player.playField.Count;

        // Check for TokenCanTribute passive (e.g., Tree of Abundance allows herbs as tributes)
        foreach (Card c in player.playField) {
            if (c.passiveEffects == null) continue;
            foreach (PassiveEffect pe in c.passiveEffects) {
                if (pe.passive != Passive.TokenCanTribute) continue;
                if (pe.tokenType == null) continue;
                // Count matching tokens as tribute value
                int matchingTokens = player.tokens.Count(t => t.tokenType == pe.tokenType);
                tributeValue += matchingTokens;
            }
        }

        // Check for tribute multipliers from alternateCosts
        if (cardRequiringTribute.alternateCosts != null) {
            foreach (AlternateCost altCost in cardRequiringTribute.alternateCosts) {
                if (altCost.altCostType != AltCostType.TributeMultiplier) continue;

                // Count matching tokens (not yet summons)
                // Skip tokens if cardType is specified (e.g., "summon" means only summons count, not tokens)
                int matchingTokens = 0;
                if (altCost.cardType == null) {
                    foreach (Token token in player.tokens) {
                        if (altCost.tokenType != null && token.tokenType == altCost.tokenType) {
                            matchingTokens++;
                        } else if (altCost.tribe != null && token.tribe == altCost.tribe) {
                            matchingTokens++;
                        }
                    }
                }

                // Each matching token counts as 'amount' tributes (full amount since tokens don't count in base)
                tributeValue += matchingTokens * altCost.amount;

                // Count matching summons on field
                int matchingSummons = 0;
                foreach (Card summon in player.playField) {
                    bool matches = false;
                    if (altCost.tokenType != null && summon is Token t && t.tokenType == altCost.tokenType) {
                        matches = true;
                    } else if (altCost.tribe != null && summon.tribe == altCost.tribe) {
                        matches = true;
                    } else if (altCost.cardType != null && summon.type == altCost.cardType) {
                        matches = true;
                    }
                    if (matches) matchingSummons++;
                }

                // Each matching summon already counts as 1, add (amount - 1) more
                tributeValue += matchingSummons * (altCost.amount - 1);
            }
        }

        return tributeValue;
    }

    /// <summary>
    /// Checks if the player can pay any Sacrifice-type alternate cost for the card.
    /// </summary>
    public static bool CanPaySacrificeAlternateCost(Player player, Card card) {
        if (card.alternateCosts == null) return false;

        foreach (AlternateCost altCost in card.alternateCosts) {
            if (altCost.altCostType != AltCostType.Sacrifice) continue;

            // Count matching tokens/cards that can be sacrificed
            int matchingCount = 0;

            if (altCost.tokenType != null) {
                matchingCount = player.tokens.Count(t => t.tokenType == altCost.tokenType);
            } else if (altCost.tribe != null) {
                matchingCount = player.tokens.Count(t => t.tribe == altCost.tribe);
                matchingCount += player.playField.Count(c => c.tribe == altCost.tribe);
            } else if (altCost.cardType != null) {
                matchingCount = player.playField.Count(c => c.type == altCost.cardType);
            }

            if (matchingCount >= altCost.amount) return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the player can pay any ExileFromHand-type alternate cost for the card.
    /// </summary>
    public static bool CanPayExileFromHandAlternateCost(Player player, Card card) {
        if (card.alternateCosts == null) return false;

        foreach (AlternateCost altCost in card.alternateCosts) {
            if (altCost.altCostType != AltCostType.ExileFromHand) continue;

            // Count matching cards in hand (excluding the card being cast)
            int matchingCount = 0;
            foreach (Card c in player.hand) {
                if (c.uid == card.uid) continue; // Don't count the card being cast
                bool matches = true;
                if (altCost.tribe != null && c.tribe != altCost.tribe) matches = false;
                if (altCost.cardType != null && c.type != altCost.cardType) matches = false;
                if (matches) matchingCount++;
            }

            if (matchingCount >= altCost.amount) return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the list of UIDs that can be sacrificed for an alternate cost.
    /// </summary>
    public static List<int> GetSacrificeAlternateCostTargets(Player player, AlternateCost altCost) {
        List<int> targets = new();

        if (altCost.tokenType != null) {
            targets.AddRange(player.tokens.Where(t => t.tokenType == altCost.tokenType).Select(t => t.uid));
        } else if (altCost.tribe != null) {
            targets.AddRange(player.tokens.Where(t => t.tribe == altCost.tribe).Select(t => t.uid));
            targets.AddRange(player.playField.Where(c => c.tribe == altCost.tribe).Select(c => c.uid));
        } else if (altCost.cardType != null) {
            targets.AddRange(player.playField.Where(c => c.type == altCost.cardType).Select(c => c.uid));
        }

        return targets;
    }

    /// <summary>
    /// Gets the list of UIDs that can be exiled from hand for an alternate cost.
    /// Excludes the card being cast.
    /// </summary>
    public static List<int> GetExileFromHandAlternateCostTargets(Player player, AlternateCost altCost, Card cardBeingCast) {
        List<int> targets = new();

        foreach (Card c in player.hand) {
            // Don't include the card being cast
            if (c.uid == cardBeingCast.uid) continue;

            bool matches = true;
            if (altCost.tribe != null && c.tribe != altCost.tribe) matches = false;
            if (altCost.cardType != null && c.type != altCost.cardType) matches = false;

            if (matches) targets.Add(c.uid);
        }

        return targets;
    }

    /// <summary>
    /// Gets the list of UIDs that can be discarded for an alternate cost (activated abilities).
    /// </summary>
    public static List<int> GetDiscardAlternateCostTargets(Player player, AlternateCost altCost) {
        List<int> targets = new();

        foreach (Card c in player.hand) {
            bool matches = true;
            if (altCost.tribe != null && c.tribe != altCost.tribe) matches = false;
            if (altCost.cardType != null && c.type != altCost.cardType) matches = false;

            if (matches) targets.Add(c.uid);
        }

        return targets;
    }

    public static DeckDestinationType ZoneToDestinationType(Zone zone) {
        return zone switch {
            Zone.Deck => DeckDestinationType.Top,
            Zone.Play => DeckDestinationType.Play,
            Zone.Hand => DeckDestinationType.Hand,
            Zone.Graveyard => DeckDestinationType.Graveyard,
            Zone.Exile => DeckDestinationType.Exile,
            Zone.Token => DeckDestinationType.Play,
            _ => throw new ArgumentOutOfRangeException(nameof(zone), zone, null)
        };
    }


    private static bool StackContains(Card c, GameMatch gameMatch) {
        foreach (StackObj stackObj in gameMatch.stack) {
            if (stackObj.sourceCard.uid == c.uid) {
                return true;
            }
        }
        return false;
    }

    public static string CapitalizeFirstLetter(string input) {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input[1..];
    }
    
    public static string MarkChosenToken(string description, int groupIndex, int choiceIndex)
    {
        return MarkChosenTokens(description, groupIndex, new List<int> { choiceIndex });
    }

    public static string MarkChosenTokens(string description, int groupIndex, List<int> choiceIndices)
    {
        string pattern = $@"\{{c{groupIndex}\}}";
        var matches = Regex.Matches(description, pattern);

        if (choiceIndices.Count == 0 || choiceIndices.All(i => i < 0 || i >= matches.Count))
            return Regex.Replace(description, pattern, ""); // fallback: remove all

        int currentIndex = 0;
        int offset = 0;

        // replace each non-chosen with "" and chosen tokens with {c}, followed by {ce} at the end of the line
        foreach (Match match in matches)
        {
            int index = match.Index + offset;
            string replacement;

            if (choiceIndices.Contains(currentIndex))
            {
                replacement = "{c}";
                description = description.Remove(index, match.Length).Insert(index, replacement);
                offset += replacement.Length - match.Length;

                // Insert {ce} at end of line
                int lineEnd = description.IndexOf('\n', index);
                if (lineEnd == -1) lineEnd = description.Length;
                description = description.Insert(lineEnd, "{ce}");
                offset += 4; // account for inserted "{ce}"
            }
            else
            {
                replacement = "";
                description = description.Remove(index, match.Length).Insert(index, replacement);
                offset += replacement.Length - match.Length;
            }

            currentIndex++;
        }

        return description;
    }
}