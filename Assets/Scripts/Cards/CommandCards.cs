using System;
using System.Collections.Generic;
using System.Linq;
using Expansions;
using player;
using TurnPhases;
using UnityEngine;

namespace Cards
{
    // When a command card may be played (Risk 2210 A.D.).
    public enum CardTiming
    {
        NotImplemented,      // card exists in a deck but can't be played yet
        BeforeFirstInvasion, // your turn, before you declare your first invasion
        OpponentInvades,     // reaction window (RW-2..5)
        EndOfTurn,
        EndOfGame,
    }

    // Per-card data. Costs here are OUR numbers (card names/texts will be rewritten before release anyway).
    public readonly struct CardSpec
    {
        public readonly int PowerUpCost;
        public readonly CardTiming Timing;
        public readonly bool OwnerIsFirstArgument; // ability's first parameter is the player who plays it
        public readonly Func<Player, object[], string> CheckTargets; // null = no targets / OK

        public CardSpec(int powerUpCost, CardTiming timing, bool ownerIsFirstArgument = false,
            Func<Player, object[], string> checkTargets = null)
        {
            PowerUpCost = powerUpCost;
            Timing = timing;
            OwnerIsFirstArgument = ownerIsFirstArgument;
            CheckTargets = checkTargets;
        }

        private static string OwnTerritory(Player p, object[] targets) =>
            targets is { Length: 1 } && targets[0] is Map.Territory t && t.Owner == p
                ? null
                : "Choose one territory you occupy.";

        // Pilot: only Energy Crisis is playable. Add cards here one at a time, each with a test.
        public static CardSpec For(CardType type) => type switch
        {
            // Power-up costs match the 2210 decks as modelled in the reference Java version (avioane/InvadeEarth).
            // Diplomat
            CardType.EnergyCrisis => new CardSpec(0, CardTiming.BeforeFirstInvasion, ownerIsFirstArgument: true),
            // Land
            CardType.AssembleModsL => new CardSpec(1, CardTiming.BeforeFirstInvasion, checkTargets: OwnTerritory),
            _ => new CardSpec(0, CardTiming.NotImplemented),
        };
    }

    // Risk 2210 command card rules (replaces the classic "draw a card when you conquer, trade sets for troops"):
    //  - Buy: 1 energy each, at most 4 per turn, only from the deck of a commander you have in play,
    //    on your turn before your first invasion. Bought cards are random from that deck.
    //  - Play: you need that commander in play, enough energy for the card's power-up cost, the right timing,
    //    and not be under Frequency Jam. Played cards go to the discard pile and are never reshuffled.
    public class CommandCards
    {
        public const int BuyCost = 1;
        public const int MaxBuysPerTurn = 4;

        private readonly GameManager _gm;
        private readonly CardRepository _cr;
        private readonly System.Random _rng = new();

        public int BuysThisTurn { get; private set; }
        public IReadOnlyList<Card> DiscardPile => _discard;
        private readonly List<Card> _discard = new();

        public Action<Player, Card> OnCardBought;
        public Action<Player, Card> OnCardPlayed;

        public CommandCards(GameManager gm, CardRepository cr)
        {
            _gm = gm;
            _cr = cr;
        }

        public void OnTurnStarted() => BuysThisTurn = 0;

        // Our map has only land territories, so the Naval and Space decks (water / Moon cards) are out of play.
        public static bool IsDeckInPlay(CommanderType deck) => DeckLists.DecksInPlay.Contains(deck);

        public int CardsLeftInDeck(CommanderType deck) => _cr.CardsInDeck.Count(c => c.Commander == deck);

        // null = allowed, otherwise the reason (shown to the player / logged).
        public string WhyCantBuy(Player p, CommanderType deck)
        {
            if (!IsDeckInPlay(deck)) return $"The {deck} deck isn't used: this map has no water or Moon territories.";
            if (!IsOwnTurnBeforeFirstInvasion(p)) return "You can only buy cards on your turn, before your first invasion.";
            if (!p.HasCommanderInPlay(deck)) return $"You need your {deck} in play to buy from its deck.";
            if (BuysThisTurn >= MaxBuysPerTurn) return $"You can buy at most {MaxBuysPerTurn} cards per turn.";
            if (p.GetEnergy() < BuyCost) return $"Buying a card costs {BuyCost} energy.";
            if (CardsLeftInDeck(deck) == 0) return $"The {deck} deck is empty.";
            return null;
        }

        public string WhyCantPlay(Player p, Card card)
        {
            if (card == null) return "No card.";
            if (!p.Cards.Contains(card)) return $"{p.Name} doesn't hold {card.Name}.";
            if (card.Timing == CardTiming.NotImplemented) return $"{card.Name} can't be played yet (not implemented).";
            if (!p.canPlayCommandCards) return "You are jammed (Frequency Jam) and can't play command cards this turn.";
            if (!p.HasCommanderInPlay(card.Commander)) return $"You need your {card.Commander} in play to play {card.Name}.";
            if (p.GetEnergy() < card.PowerUpCost) return $"{card.Name} costs {card.PowerUpCost} energy to play.";
            if (card.Timing == CardTiming.BeforeFirstInvasion && !IsOwnTurnBeforeFirstInvasion(p))
                return $"{card.Name} can only be played on your turn, before your first invasion.";
            if (card.Timing != CardTiming.BeforeFirstInvasion)
                return $"{card.Name} ({card.Timing}) can't be played yet.";
            return null;
        }

        public string WhyCantPlay(Player p, Card card, object[] targets) =>
            WhyCantPlay(p, card) ?? card.Spec.CheckTargets?.Invoke(p, targets ?? Array.Empty<object>());

        public Card Buy(Player p, CommanderType deck)
        {
            var why = WhyCantBuy(p, deck);
            if (why != null) throw new InvalidOperationException(why);

            var choices = _cr.CardsInDeck.Where(c => c.Commander == deck).ToList();
            var card = choices[_rng.Next(choices.Count)];
            _cr.CardsInDeck.Remove(card);
            p.SetEnergy(p.GetEnergy() - BuyCost);
            p.AddCard(card);
            BuysThisTurn++;
            Debug.Log($"{p.Name} bought a {deck} card ({BuysThisTurn}/{MaxBuysPerTurn} this turn).");
            OnCardBought?.Invoke(p, card);
            return card;
        }

        public void Play(Player p, Card card, object[] targets)
        {
            var why = WhyCantPlay(p, card, targets);
            if (why != null) throw new InvalidOperationException(why);

            p.SetEnergy(p.GetEnergy() - card.PowerUpCost);
            p.RemoveCard(card);
            _discard.Add(card); // never reshuffled
            var args = card.Spec.OwnerIsFirstArgument
                ? new object[] { p }.Concat(targets ?? Array.Empty<object>()).ToArray()
                : targets ?? Array.Empty<object>();
            Debug.Log($"{p.Name} played {card.Name} (cost {card.PowerUpCost}).");
            card.UseAbility(args);
            OnCardPlayed?.Invoke(p, card);
        }

        private bool IsOwnTurnBeforeFirstInvasion(Player p)
        {
            if (_gm.IsBidding || _gm.GameState != GameState.Playing || _gm.CurrentPlayer != p) return false;
            return _gm.CurrentPhase switch
            {
                ReinforcePhase => true,
                AttackPhase ap => !ap.AttackResults.Any() && ap.PendingAttack == null,
                _ => false
            };
        }
    }
}
