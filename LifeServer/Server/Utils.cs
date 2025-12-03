using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Authorization;
using Newtonsoft.Json;
using Server.CardProperties;

namespace Server;

public class Utils {
    
    
    public static string GetCardJson(int cardId) {
        string result = cardId.ToString("D3");
        string directoryPath = @"C:\Users\bango\OneDrive\Desktop\MetaJet Games\Lifeburn\Data\Cards";
        string[] matchingFiles = Directory.GetFiles(directoryPath, result + "*");
        using (StreamReader streamReader = new StreamReader(matchingFiles[0], Encoding.UTF8))
        {
            return streamReader.ReadToEnd().Replace("\r\n", Environment.NewLine);
        }
    }

    public static bool CheckPlayability(Card card, GameMatch gameMatch, Player player) {
        if (card.type == CardType.Token) return false;
        if (!player.hand.Contains(card)) {
            if (card.activatedEffects != null) {
                foreach (ActivatedEffect aEffect in card.activatedEffects) {
                    if (!aEffect.CostIsAvailable(gameMatch, player)) continue;
                    return true;
                }
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
                // must have enough life
                if (player.lifeTotal <= card.GetCost()) return false;
                break;
            case CardType.Object:
                // must have enough life
                if(player.lifeTotal <= card.GetCost()) return false;
                // must be your turn
                if (gameMatch.turnPlayerId != player.playerId) return false;
                // must be main phase
                if (gameMatch.currentPhase is not (Phase.Main or Phase.SecondMain)) return false;
                // stack must be empty
                if (gameMatch.stack.Count > 0) return false;
                break;
            case CardType.Summon:
                // must be your turn
                if (gameMatch.turnPlayerId != player.playerId) return false;
                // must have enough summons to tribute
                if (card.GetCost() > player.playField.Count) return false;
                // must be a main phase
                if (gameMatch.currentPhase is not (Phase.Main or Phase.SecondMain)) return false;
                // stack must be empty
                if (gameMatch.stack.Count > 0) return false;
                // must be your first summon this turn unless the card has the ByPassSummonLimit passive
                if (player.turnSummonCount > 0 && !hasBypassSummonLimit) return false;
                break;
        }
        
        // cards with required targets must have targets available
        if (card.stackEffects != null) {
            foreach (Effect sEffect in card.stackEffects) {
                if (sEffect.targetType != null && gameMatch.GetPossibleTargets(player, sEffect).Count == 0) return false;
            }
        }
        // all additional costs must be available
        if (card.additionalCosts != null) {
            if (card.additionalCosts.Any(aCost => !aCost.CostIsAvailable(gameMatch, player))) return false;
        }
        // all conditions are met
        return true;
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
        string pattern = $@"\{{c{groupIndex}\}}";
        var matches = Regex.Matches(description, pattern);
    
        if (choiceIndex < 0 || choiceIndex >= matches.Count)
            return Regex.Replace(description, pattern, ""); // fallback: remove all
        
        int currentIndex = 0;
        int offset = 0;
        
        // replace each non-chosen with "" and the chosen token with {c}, followed by {ce} at the end of the line
        foreach (Match match in matches)
        {
            int index = match.Index + offset;
            string replacement;

            if (currentIndex == choiceIndex)
            {
                replacement = "{c}";
                description = description.Remove(index, match.Length).Insert(index, replacement);
                offset += replacement.Length - match.Length;

                // Insert {ec} at end of line
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