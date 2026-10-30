using System;
using System.Collections.Generic;
using System.Linq;
using Decisions;
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

        private static string OwnTerritoryForStation(Player p, object[] targets)
        {
            if (OwnTerritory(p, targets) is { } why) return why;
            if (((Map.Territory)targets[0]).containsSpaceStation) return "That territory already has a space station.";
            if (Purchases.StationsOf(p) >= Purchases.MaxStations) return $"You already have {Purchases.MaxStations} space stations.";
            return null;
        }

        private static string ThreeDifferentOwnTerritories(Player p, object[] targets) =>
            targets is { Length: 3 } && targets.All(x => x is Map.Territory t && t.Owner == p) && targets.Distinct().Count() == 3
                ? null
                : "Choose three different territories you occupy.";

        private static string AnOpponent(Player p, object[] targets) =>
            targets is { Length: 1 } && targets[0] is Player o && o != p && !o.IsDead()
                ? null
                : "Choose an opponent.";

        private static string OpponentTerritory(Player p, object[] targets) =>
            targets is { Length: 1 } && targets[0] is Map.Territory t && t.Owner != p
                ? null
                : "Choose an opponent's territory.";

        private static string OpponentCommander(Player p, object[] targets) =>
            targets is { Length: 2 } && targets[0] is Map.Territory t && t.Owner != p && targets[1] is CommanderType type
            && t.CommandersList.Any(c => c.CommanderType == type)
                ? null
                : "Choose an opponent's commander.";

        // Pilot: only Energy Crisis is playable. Add cards here one at a time, each with a test.
        public static CardSpec For(CardType type) => type switch
        {
            // Power-up costs match the 2210 decks as modelled in the reference Java version (avioane/InvadeEarth).
            // Diplomat
            CardType.EnergyCrisis => new CardSpec(0, CardTiming.BeforeFirstInvasion, ownerIsFirstArgument: true),
            CardType.CeaseFire => new CardSpec(2, CardTiming.OpponentInvades),
            CardType.TerritorialStation => new CardSpec(1, CardTiming.BeforeFirstInvasion, checkTargets: OwnTerritoryForStation),
            CardType.ModReduction => new CardSpec(2, CardTiming.BeforeFirstInvasion, ownerIsFirstArgument: true),
            CardType.Redeployment => new CardSpec(0, CardTiming.EndOfTurn),
            CardType.ColonyInfluenceD => new CardSpec(0, CardTiming.EndOfGame),
            CardType.Evacuation => new CardSpec(0, CardTiming.OpponentInvades),
            // Land
            CardType.AssembleModsL => new CardSpec(1, CardTiming.BeforeFirstInvasion, checkTargets: OwnTerritory),
            CardType.StealthModsL => new CardSpec(0, CardTiming.OpponentInvades),
            CardType.ReinforcementsL => new CardSpec(0, CardTiming.BeforeFirstInvasion, checkTargets: ThreeDifferentOwnTerritories),
            CardType.FrequencyJamL => new CardSpec(0, CardTiming.BeforeFirstInvasion, checkTargets: AnOpponent),
            CardType.ScoutForces => new CardSpec(0, CardTiming.BeforeFirstInvasion),
            CardType.ColonyInfluenceL => new CardSpec(0, CardTiming.EndOfGame),
            // Nuclear (land variants)
            CardType.RocketStrikeLand => new CardSpec(2, CardTiming.BeforeFirstInvasion, checkTargets: OpponentTerritory),
            CardType.ScatterBombLand => new CardSpec(1, CardTiming.BeforeFirstInvasion, ownerIsFirstArgument: true),
            CardType.TheMother => new CardSpec(3, CardTiming.BeforeFirstInvasion),
            CardType.AssassinBomb => new CardSpec(1, CardTiming.BeforeFirstInvasion, checkTargets: OpponentCommander),
            // Not implemented yet: DecoyRevealed (move any commanders anywhere: needs a multi-step UI),
            // Armageddon (every player plays nuclear cards in turn order: needs its own turn flow).
            CardType.LandDeathTrap => new CardSpec(3, CardTiming.OpponentInvades),
            CardType.StealthStation => new CardSpec(0, CardTiming.OpponentInvades),
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

        // ---------------- MOD Reduction ----------------
        // "All of your opponents must remove 4 MODs in turn order. Then you remove 2 MODs."
        // Each player picks: one RemoveUnits decision per unit, addressed to that player (their own device online;
        // bots answer instantly). A territory always keeps at least 1 unit.
        public const int ModReductionOpponents = 4;
        public const int ModReductionOwner = 2;
        private readonly Queue<(Player player, int count)> _removals = new();
        private Player _removingPlayer;
        public int RemovalsLeft { get; private set; }
        public Player RemovingPlayer => _removingPlayer;
        public Action OnModReductionFinished;

        private void StartModReduction(Player owner)
        {
            foreach (var opponent in _gm.GetPlayersInTurnOrder().Where(o => o != owner && !o.IsDead()))
                _removals.Enqueue((opponent, ModReductionOpponents));
            _removals.Enqueue((owner, ModReductionOwner));
            NextRemoval();
        }

        private void NextRemoval()
        {
            while (_removals.Count > 0)
            {
                var (player, count) = _removals.Dequeue();
                _removingPlayer = player;
                RemovalsLeft = count;
                if (AskRemoval()) return;
            }
            _removingPlayer = null;
            RemovalsLeft = 0;
            OnModReductionFinished?.Invoke();
        }

        private bool AskRemoval()
        {
            var options = _removingPlayer.Territories.Where(t => t.Troops > 1).Select(t => t.Name).ToList();
            if (RemovalsLeft <= 0 || options.Count == 0) return false;
            _gm.OpenDecision(new PendingDecision(_gm.GetPlayerId(_removingPlayer), DecisionKind.RemoveUnits, options),
                OnRemovalAnswered);
            return true;
        }

        private void OnRemovalAnswered(Actions.DecisionResponseAction response)
        {
            var territory = Map.TerritoryRepository.Instance.FromName(response.Option);
            territory.RemoveTroops(1);
            RemovalsLeft--;
            Debug.Log($"MOD Reduction: {response.Player.Name} removed 1 unit from {territory.Name} ({RemovalsLeft} left).");
            if (!AskRemoval()) NextRemoval();
        }

        // ---------------- Scout Forces ----------------
        // "Draw a land territory card and secretly place it facedown. Place 5 MODs on it. When you occupy this
        //  territory, immediately place the MODs." We pick a random territory the player doesn't occupy.
        private readonly Dictionary<Map.Territory, Player> _scouted = new();
        public IEnumerable<Map.Territory> ScoutedBy(Player p) => _scouted.Where(kv => kv.Value == p).Select(kv => kv.Key).ToList();
        public const int ScoutForcesUnits = 5;

        private void Scout(Player p)
        {
            var candidates = Map.TerritoryRepository.Instance.Territories.Where(t => t.Owner != p && !_scouted.ContainsKey(t)).ToList();
            if (candidates.Count == 0) return;
            var t = candidates[_rng.Next(candidates.Count)];
            _scouted[t] = p;
            t.OnOwnerChanged += OnScoutedOwnerChanged(t);
        }

        private Action<Player, Player> OnScoutedOwnerChanged(Map.Territory t)
        {
            Action<Player, Player> handler = null;
            handler = (_, newOwner) =>
            {
                if (!_scouted.TryGetValue(t, out var scout) || newOwner != scout) return;
                _scouted.Remove(t);
                t.OnOwnerChanged -= handler;
                t.AddTroops(ScoutForcesUnits);
                Debug.Log($"Scout Forces: {scout.Name} occupied {t.Name} and placed {ScoutForcesUnits} MODs.");
            };
            return handler;
        }

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
            switch (card.Timing)
            {
                case CardTiming.BeforeFirstInvasion:
                    if (!IsOwnTurnBeforeFirstInvasion(p)) return $"{card.Name} can only be played on your turn, before your first invasion.";
                    return null;
                case CardTiming.EndOfTurn:
                    if (_gm.CurrentPlayer != p || !(_gm.CurrentPhase is FortifyPhase))
                        return $"{card.Name} is played at the end of your turn, after attacking (Fortify).";
                    return null;
                case CardTiming.EndOfGame:
                    return $"{card.Name} counts automatically in the final score (if its commander is alive).";
                default:
                    return $"{card.Name} is played when an opponent invades you.";
            }
        }

        public string WhyCantPlay(Player p, Card card, object[] targets) =>
            WhyCantPlay(p, card) ?? card.Spec.CheckTargets?.Invoke(p, targets ?? Array.Empty<object>());

        // ---------------- Reaction cards ("play after an opponent declares an invasion into a territory you occupy") ----------------

        public const string CardOptionPrefix = "Card:";
        public static string OptionFor(Card card) => CardOptionPrefix + card.Id;

        public Card CardFromOption(Player p, string option)
        {
            if (p == null || option == null || !option.StartsWith(CardOptionPrefix)) return null;
            return int.TryParse(option.Substring(CardOptionPrefix.Length), out var id) ? p.Cards.FirstOrDefault(c => c.Id == id) : null;
        }

        public IEnumerable<Card> PlayableReactions(Player defender, Actions.AttackAction attack) =>
            defender == null ? Enumerable.Empty<Card>() : defender.Cards.Where(c => WhyCantReact(defender, c, attack) == null).ToList();

        public string WhyCantReact(Player p, Card card, Actions.AttackAction attack)
        {
            if (attack == null) return "There is no invasion to react to.";
            if (card == null || !p.Cards.Contains(card)) return $"{p.Name} doesn't hold that card.";
            if (card.Timing != CardTiming.OpponentInvades) return $"{card.Name} isn't played in response to an invasion.";
            if (attack.Target.Owner != p) return "Only the defender can react.";
            if (!p.canPlayCommandCards) return "You are jammed (Frequency Jam) and can't play command cards this turn.";
            if (!p.HasCommanderInPlay(card.Commander)) return $"You need your {card.Commander} in play to play {card.Name}.";
            if (p.GetEnergy() < card.PowerUpCost) return $"{card.Name} costs {card.PowerUpCost} energy to play.";
            switch (card.CardType)
            {
                case CardType.StealthStation:
                    if (attack.Target.containsSpaceStation) return $"{attack.Target.Name} already has a space station.";
                    if (Purchases.StationsOf(p) >= Purchases.MaxStations) return $"You already have {Purchases.MaxStations} space stations.";
                    break;
                case CardType.Evacuation:
                    if (p.GetTerritoryCount() <= 1) return "You have nowhere to evacuate to.";
                    break;
            }
            return null;
        }

        // Where Evacuation sends the units: the defender's strongest other territory (no path needed).
        public static Map.Territory EvacuationDestination(Player p, Map.Territory attacked) =>
            p.Territories.Where(t => t != attacked).OrderByDescending(t => t.Troops).FirstOrDefault();

        // Plays a reaction card. Returns true if the invasion is cancelled (Cease Fire).
        public bool PlayReaction(Player p, Card card, Actions.AttackAction attack)
        {
            var why = WhyCantReact(p, card, attack);
            if (why != null) throw new InvalidOperationException(why);

            p.SetEnergy(p.GetEnergy() - card.PowerUpCost);
            p.RemoveCard(card);
            _discard.Add(card);
            Debug.Log($"{p.Name} plays {card.Name} against {attack.Player.Name}'s invasion of {attack.Target.Name}.");

            var cancelled = false;
            switch (card.CardType)
            {
                case CardType.StealthModsL:
                case CardType.StealthStation:
                    card.UseAbility(attack.Target);
                    break;
                case CardType.LandDeathTrap:
                    card.UseAbility(attack.Origin);
                    break;
                case CardType.Evacuation:
                    card.UseAbility(attack.Target, EvacuationDestination(p, attack.Target));
                    break;
                case CardType.CeaseFire:
                    cancelled = true; // AttackPhase stops the invasion and blocks further attacks on p this turn
                    break;
            }
            OnCardPlayed?.Invoke(p, card);
            return cancelled;
        }

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
            switch (card.CardType)
            {
                case CardType.ScoutForces:
                    Scout(p);
                    break;
                case CardType.Redeployment:
                    _gm.FortifyPhase.AddExtraMove();
                    break;
                case CardType.ModReduction:
                    StartModReduction(p);
                    break;
                default:
                    card.UseAbility(args);
                    break;
            }
            OnCardPlayed?.Invoke(p, card);
        }

        private bool IsOwnTurnBeforeFirstInvasion(Player p) => _gm.IsCommandStep(p);
    }
}
