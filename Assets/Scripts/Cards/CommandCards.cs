using System;
using System.Collections.Generic;
using System.Linq;
using Decisions;
using Expansions;
using InvadeEarth.Rules;
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

        static string OwnOfTerrain(Player p, object[] targets)
        {
            if (OwnTerritory(p, targets) is { } why) return why;
            return TerrainOk((Map.Territory)targets[0]) ? null : "That territory is the wrong kind for this card.";
        }

        static string ThreeOfTerrain(Player p, object[] targets)
        {
            if (ThreeDifferentOwnTerritories(p, targets) is { } why) return why;
            return targets.All(x => TerrainOk((Map.Territory)x)) ? null : "All three have to be the right kind of territory.";
        }

        // Hold 3 or more: pick 3. Hold 1 or 2: the card places 1 unit on each of those.
        static string ReinforcementsTargets(Player p, object[] targets)
        {
            var need = Map.Frontiers.CardTerrain(_checking);
            var held = p.Territories.Where(t => need == null || t.Kind == need).ToList();
            if (held.Count == 0) return "You don't hold any territories of that kind.";
            var want = System.Math.Min(3, held.Count);
            if (targets == null || targets.Length != want)
                return want == 3
                    ? "Choose three different territories you occupy."
                    : $"You hold {want}. Place 1 unit on each.";
            if (targets.Distinct().Count() != want) return "Choose different territories.";
            foreach (var x in targets)
            {
                if (x is not Map.Territory t || t.Owner != p) return "Choose territories you occupy.";
                if (!TerrainOk(t)) return "That territory is the wrong kind for this card.";
            }
            return null;
        }

        static string OpponentOfTerrain(Player p, object[] targets)
        {
            if (OpponentTerritory(p, targets) is { } why) return why;
            return TerrainOk((Map.Territory)targets[0]) ? null : "That territory is the wrong kind for this card.";
        }

        // The card being checked is not in scope here, so the caller passes it by closing over CardSpec.For.
        // TerrainOk is set for the duration of one check.
        static CardType _checking;

        static bool TerrainOk(Map.Territory t)
        {
            var need = Map.Frontiers.CardTerrain(_checking);
            return need == null || t.Kind == need;
        }

        private static string OpponentTerritory(Player p, object[] targets) =>
            targets is { Length: 1 } && targets[0] is Map.Territory t && t.Owner != p
                ? null
                : "Choose an opponent's territory.";

        private static string OpponentCommander(Player p, object[] targets) =>
            targets is { Length: 2 } && targets[0] is Map.Territory t && t.Owner != p && targets[1] is CommanderType type
            && t.CommandersList.Any(c => c.CommanderType == type)
                ? null
                : "Choose an opponent's commander.";

        // Decoys Revealed: (from, commander, to) triples, applied in order. Both ends must be yours.
        public static string DecoyMoves(Player p, object[] targets)
        {
            if (targets == null || targets.Length < 3 || targets.Length % 3 != 0)
                return "Move at least one commander: click where it is, then where it goes.";
            var at = new Dictionary<Map.Territory, List<CommanderType>>();
            List<CommanderType> In(Map.Territory t)
            {
                if (!at.TryGetValue(t, out var list))
                    at[t] = list = t.CommandersList.Select(c => c.CommanderType).ToList();
                return list;
            }
            for (var i = 0; i < targets.Length; i += 3)
            {
                if (!(targets[i] is Map.Territory from && targets[i + 1] is CommanderType type && targets[i + 2] is Map.Territory to))
                    return "Each move is a territory, a commander and a territory.";
                if (from.Owner != p || to.Owner != p) return "Commanders can only move between territories you occupy.";
                if (!Expansions.Purchases.CanStand(type, to)) return "That commander can't move onto that territory.";
                if (from == to) return "Pick a different territory to move it to.";
                if (!In(from).Remove(type)) return $"There is no {type} of yours in {from.Name}.";
                In(to).Add(type);
            }
            return null;
        }

        // Pilot: only Energy Crisis is playable. Add cards here one at a time, each with a test.
        public static CardSpec For(CardType type)
        {
            var rule = CardCatalog.For(type.ToString());
            var timing = (CardTiming)Enum.Parse(typeof(CardTiming), rule.Timing.ToString());
            var inner = CheckFor(type);
            Func<Player, object[], string> check = inner == null ? null
                : (p, targets) => { _checking = type; return inner(p, targets); };
            return new CardSpec(rule.PowerUpCost, timing, OwnerGoesFirst(type), check);
        }

        static bool OwnerGoesFirst(CardType type) =>
            type is CardType.EnergyCrisis or CardType.ModReduction
                or CardType.ScatterBombLand or CardType.ScatterBombWater or CardType.ScatterBombMoon;

        static Func<Player, object[], string> CheckFor(CardType type) => type switch
        {
            CardType.TerritorialStation => OwnTerritoryForStation,
            CardType.AssembleModsL or CardType.AssembleModsNa or CardType.AssembleModsS => OwnOfTerrain,
            CardType.ReinforcementsL or CardType.ReinforcementsNa or CardType.ReinforcementsS => ReinforcementsTargets,
            CardType.FrequencyJamL or CardType.FrequencyJamNa or CardType.FrequencyJamS => AnOpponent,
            CardType.RocketStrikeLand or CardType.RocketStrikeWater or CardType.RocketStrikeMoon => OpponentOfTerrain,
            CardType.AssassinBomb => OpponentCommander,
            CardType.DecoyRevealed => DecoyMoves,
            _ => null,
        };
    }

    // Risk 2210 command card rules (replaces the classic "draw a card when you conquer, trade sets for troops"):
    //  - Buy: 1 energy each, at most 4 per turn, only from the deck of a commander you have in play,
    //    on your turn before your first invasion. Bought cards are random from that deck.
    //  - Play: you need that commander in play, enough energy for the card's power-up cost, the right timing,
    //    and not be under Frequency Jam. Played cards go to the discard pile and are never reshuffled.
    public class CommandCards
    {
        public const int BuyCost = CardCatalog.BuyCost;
        public const int MaxBuysPerTurn = CardCatalog.MaxBuysPerTurn;

        private readonly GameManager _gm;
        private readonly CardRepository _cr;

        public int BuysThisTurn { get; private set; }
        public IReadOnlyList<Card> DiscardPile => _discard;

        // An eliminated player's cards are discarded. They are not given to the player who took the last territory.
        public void Discard(System.Collections.Generic.IEnumerable<Card> cards)
        {
            if (cards == null) return;
            foreach (var card in cards)
                if (card != null) _discard.Add(card);
        }
        private readonly List<Card> _discard = new();

        public Action<Player, Card> OnCardBought;
        public Action<Player, Card> OnCardPlayed;
        public Action<CardPlayReport> OnCardReport;
        public CardPlayReport LastReport { get; private set; }

        private static Dictionary<Map.Territory, int> UnitsNow() =>
            Map.TerritoryRepository.Instance != null
                ? Map.TerritoryRepository.Instance.Territories.ToDictionary(t => t, t => t.Troops)
                : new Dictionary<Map.Territory, int>();

        private void Report(Player p, Card card, object[] targets, Dictionary<Map.Territory, int> before, bool reaction)
        {
            var ab = _cr.Abilities;
            var report = new CardPlayReport { Player = p, Card = card, Targets = targets, Reaction = reaction, Note = _note };
            _note = null;
            switch (card.CardType)
            {
                case CardType.RocketStrikeLand:
                case CardType.RocketStrikeWater:
                case CardType.RocketStrikeMoon:
                case CardType.AssassinBomb:
                    report.Roll = ab.LastRoll;
                    break;
                case CardType.TheMother:
                    report.Roll = ab.LastRoll;
                    report.Continent = ab.LastContinentHit?.Name;
                    break;
                case CardType.ScatterBombLand:
                case CardType.ScatterBombWater:
                case CardType.ScatterBombMoon:
                    report.Drawn = ab.LastScatterTargets.ToList();
                    break;
                case CardType.AquaBrother:
                case CardType.NickyBoy:
                    report.Roll = ab.LastRoll;
                    report.Drawn = ab.LastScatterTargets.ToList();
                    break;
            }
            foreach (var (t, units) in before)
                if (t.Troops != units)
                    report.Changes.Add((t, units, t.Troops));
            LastReport = report;
            OnCardReport?.Invoke(report);
        }

        public CommandCards(GameManager gm, CardRepository cr)
        {
            _gm = gm;
            _cr = cr;
        }

        public bool BuysClosed { get; private set; }

        public void OnTurnStarted()
        {
            BuysThisTurn = 0;
            BuysClosed = false;
            _surface.Clear();
        }

        public bool HasUnseen(Player p) => p != null && p.Cards.Any(c => c.Sealed);

        // Looking ends the buy. Every card bought this turn is shown at once.
        public void RevealBought(Player p)
        {
            if (p == null) return;
            foreach (var card in p.Cards)
                if (card != null && card.Sealed) card.Unseal();
            BuysClosed = true;
        }

        // ---------------- Naval: Hidden Energy, Space: Energy Extraction and Invade Surface ----------------
        // Hidden Energy draws a sea. Hold it when your turn ends and you gain 4 energy.
        // Energy Extraction pays 7 at the end of the turn for each copy, if you hold every moon territory.
        // Invade Surface draws a land you don't hold. This turn, any of your moon territories may attack it.
        public const int HiddenEnergyReward = 4;
        public const int EnergyExtractionReward = 7;

        readonly List<(Player Player, Map.Territory Sea)> _hiddenEnergy = new();
        readonly Dictionary<Player, int> _extractions = new();
        readonly List<(Player Player, Map.Territory Land)> _surface = new();
        string _note;

        public bool AllowsSurfaceAttack(Player p, Map.Territory from, Map.Territory to) =>
            from != null && to != null && from.Owner == p && from.Kind == Map.TerrainKind.Moon
            && _surface.Any(s => s.Player == p && s.Land == to);

        public IEnumerable<Map.Territory> SurfaceLands(Player p) =>
            _surface.Where(s => s.Player == p).Select(s => s.Land);

        public Action<string> OnNotice;

        public void SettleEndOfTurn(Player p)
        {
            if (p == null) return;
            foreach (var bet in _hiddenEnergy.Where(h => h.Player == p).ToList())
            {
                _hiddenEnergy.Remove(bet);
                if (bet.Sea == null || bet.Sea.Owner != p) continue;
                p.SetEnergy(p.GetEnergy() + HiddenEnergyReward);
                OnNotice?.Invoke($"{p.Name} still holds {Nice(bet.Sea.Name)}: +{HiddenEnergyReward} energy (Hidden Energy).");
            }
            if (!_extractions.TryGetValue(p, out var copies)) return;
            _extractions.Remove(p);
            var moons = Map.TerritoryRepository.Instance != null
                ? Map.TerritoryRepository.Instance.Territories.Where(t => t.Kind == Map.TerrainKind.Moon).ToList()
                : null;
            if (moons == null || moons.Count == 0 || moons.Any(t => t.Owner != p)) return;
            var gain = copies * EnergyExtractionReward;
            p.SetEnergy(p.GetEnergy() + gain);
            OnNotice?.Invoke($"{p.Name} holds the Moon: +{gain} energy (Energy Extraction).");
        }

        static string Nice(string territoryName) => (territoryName ?? "").Replace('_', ' ');

        void PlayHiddenEnergy(Player p)
        {
            var sea = Pick(Map.TerritoryRepository.Instance.Territories.Where(t => t.Kind == Map.TerrainKind.Sea));
            if (sea == null) return;
            _hiddenEnergy.Add((p, sea));
            _note = $"drew {Nice(sea.Name)}: +{HiddenEnergyReward} energy if you still hold it when the turn ends";
        }

        void PlayEnergyExtraction(Player p)
        {
            _extractions[p] = _extractions.TryGetValue(p, out var n) ? n + 1 : 1;
            _note = $"hold every moon territory when the turn ends for +{EnergyExtractionReward} energy";
        }

        void PlayInvadeSurface(Player p)
        {
            var land = Pick(Map.TerritoryRepository.Instance.Territories.Where(t => t.Kind == Map.TerrainKind.Land && t.Owner != p));
            if (land == null) return;
            _surface.Add((p, land));
            _note = $"this turn you can attack {Nice(land.Name)} from any moon territory";
        }

        static Map.Territory Pick(IEnumerable<Map.Territory> pool)
        {
            var list = pool.OrderBy(t => t.Name).ToList();
            return list.Count == 0 ? null : list[GameRandom.Current.Range(0, list.Count)];
        }

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
        // Online the territory is secret: only the host and the scout know it (Secrets.Private). Everyone knows
        // how many scouted territories each player has; when one of those players takes a territory, the host
        // says whether it was scouted.
        private readonly Dictionary<Map.Territory, Player> _scouted = new();
        private readonly Dictionary<Player, int> _scoutsLeft = new();
        private bool _watchingOwners;
        public IEnumerable<Map.Territory> ScoutedBy(Player p) => _scouted.Where(kv => kv.Value == p).Select(kv => kv.Key).ToList();
        public int ScoutsLeft(Player p) => _scoutsLeft.TryGetValue(p, out var n) ? n : 0;
        public const int ScoutForcesUnits = 5;

        private void Scout(Player p)
        {
            WatchOwners();
            Map.Territory picked = null;
            if (Secrets.Decides)
            {
                var candidates = Map.TerritoryRepository.Instance.Territories
                    .Where(t => t.Owner != p && !_scouted.ContainsKey(t)).OrderBy(t => t.Name).ToList();
                if (candidates.Count > 0) picked = candidates[Secrets.DrawRandom.Range(0, candidates.Count)];
            }
            if (!Secrets.PublicBool(() => picked != null)) return;
            var name = Secrets.Private(p, () => picked.Name);
            picked ??= name != null ? Map.TerritoryRepository.Instance.FromName(name) : null;
            if (picked != null) _scouted[picked] = p;
            _scoutsLeft[p] = ScoutsLeft(p) + 1;
        }

        private void WatchOwners()
        {
            if (_watchingOwners) return;
            _watchingOwners = true;
            foreach (var territory in Map.TerritoryRepository.Instance.Territories)
            {
                var t = territory;
                t.OnOwnerChanged += (_, newOwner) => OnOwnerChanged(t, newOwner);
            }
        }

        private void OnOwnerChanged(Map.Territory t, Player newOwner)
        {
            if (newOwner == null || ScoutsLeft(newOwner) == 0) return;
            if (!Secrets.PublicBool(() => _scouted.TryGetValue(t, out var s) && s == newOwner)) return;
            _scouted.Remove(t);
            _scoutsLeft[newOwner] = ScoutsLeft(newOwner) - 1;
            t.AddTroops(ScoutForcesUnits);
            Debug.Log($"Scout Forces: {newOwner.Name} occupied {t.Name} and placed {ScoutForcesUnits} MODs.");
        }

        // Naval and Space come in when the match built seas and a moon (MatchOptions.Frontiers).
        public static bool IsDeckInPlay(CommanderType deck) => DeckLists.DecksInPlay.Contains(deck);

        public int CardsLeftInDeck(CommanderType deck) => _cr.DeckCount(deck);

        // ---------------- Hidden hands (online) ----------------
        // Every device knows how many cards each player holds and which deck each came from. Only the host and
        // the owner know which cards they are; the other devices hold face-down cards (Card.IsHidden).

        // Takes one card for p from the cards `allowed` lets through (null if there are none).
        private Card Draw(Player p, Func<Card, bool> allowed)
        {
            Card picked = null;
            if (Secrets.Decides)
            {
                Secrets.Source?.BeforeDeciding();
                var choices = _cr.CardsInDeck.Where(allowed).OrderBy(c => c.Id).ToList();
                if (choices.Count > 0) picked = choices[Secrets.DrawRandom.Range(0, choices.Count)];
            }
            var deckName = Secrets.Public(() => picked == null ? "" : picked.Commander.ToString());
            if (string.IsNullOrEmpty(deckName)) return null;
            var deck = (CommanderType)Enum.Parse(typeof(CommanderType), deckName);
            var idText = Secrets.Private(p, () => picked.Id.ToString());
            var card = picked ?? (int.TryParse(idText, out var id) ? _cr.CardById(id) : null);

            _cr.TookFromDeck(deck);
            if (card == null)
                card = Card.FaceDown(deck);
            else
                _cr.CardsInDeck.Remove(card);
            p.AddCard(card);
            return card;
        }

        // p's face-down card turns out to be `card` (they played it, or this device may now see it).
        // True if p holds it afterwards.
        public bool Reveal(Player p, Card card)
        {
            if (p == null || card == null || card.IsHidden) return false;
            if (p.Cards.Contains(card)) return true;
            if (!_cr.CardsInDeck.Contains(card)) return false;
            var faceDown = p.Cards.FirstOrDefault(c => c.IsHidden && c.Commander == card.Commander)
                           ?? p.Cards.FirstOrDefault(c => c.IsHidden);
            if (faceDown == null) return false;
            p.RemoveCard(faceDown);
            _cr.CardsInDeck.Remove(card);
            p.AddCard(card);
            return true;
        }

        // A new online host (after the old one left) holds face-down cards it now has to know. Players who
        // reconnect say which cards they hold first; the rest (bots, players who are away) get cards from
        // the ones nobody can account for. Nobody knew those cards, so this is as good as the original.
        public void ResolveFaceDownCards(IRandom rng, Player only = null)
        {
            foreach (var p in _gm.Players)
            {
                if (only != null && p != only) continue;
                foreach (var faceDown in p.Cards.Where(c => c.IsHidden).ToList())
                {
                    var pool = _cr.CardsInDeck.Where(c => c.Commander == faceDown.Commander).OrderBy(c => c.Id).ToList();
                    if (pool.Count == 0) pool = _cr.CardsInDeck.OrderBy(c => c.Id).ToList();
                    if (pool.Count == 0) break;
                    Reveal(p, pool[rng.Range(0, pool.Count)]);
                }
            }
        }

        // The host says which cards p holds (p is this device's player). Replaces the hand this device had.
        public void SetOwnHand(Player p, IEnumerable<Card> cards)
        {
            var wanted = cards.Where(c => c != null && !c.IsHidden).Distinct().ToList();
            var old = p.Cards.ToList();
            if (old.Count == wanted.Count && old.All(wanted.Contains)) return;
            p.RemoveCards(old);
            foreach (var c in old.Where(c => !c.IsHidden)) _cr.CardsInDeck.Add(c);
            foreach (var c in wanted)
            {
                foreach (var other in _gm.Players.Where(o => o != p && o.Cards.Contains(c)))
                {
                    other.RemoveCard(c);
                    other.AddCard(Card.FaceDown(c.Commander));
                }
                _cr.CardsInDeck.Remove(c);
            }
            p.AddCards(wanted);
        }

        // null = allowed, otherwise the reason (shown to the player / logged).
        public string WhyCantBuy(Player p, CommanderType deck)
        {
            if (!IsDeckInPlay(deck)) return $"The {deck} deck isn't used: this map has no water or Moon territories.";
            if (!IsOwnTurnBeforeFirstInvasion(p)) return "You can only buy cards on your turn, before your first invasion.";
            if (_gm.ShopClosed) return "You've finished hiring and buying for this turn.";
            if (!p.HasCommanderInPlay(deck)) return $"You need your {deck} in play to buy from its deck.";
            if (BuysClosed) return "You already looked at the cards you bought. Buy them all before you look.";
            if (BuysThisTurn >= MaxBuysPerTurn) return $"You can buy at most {MaxBuysPerTurn} cards per turn.";
            if (p.GetEnergy() < BuyCost) return $"Buying a card costs {BuyCost} energy.";
            if (CardsLeftInDeck(deck) == 0) return $"The {deck} deck is empty.";
            return null;
        }

        public string WhyCantPlay(Player p, Card card)
        {
            if (card == null) return "No card.";
            if (!p.Cards.Contains(card)) return $"{p.Name} doesn't hold {card.Name}.";
            if (p.Cards.Any(c => c.Sealed)) return "Look at the cards you just bought before you play one.";
            if (card.Timing == CardTiming.NotImplemented) return $"{card.Name} can't be played yet (not implemented).";
            if (!p.canPlayCommandCards) return "You are jammed (Frequency Jam) and can't play command cards this turn.";
            if (!p.HasCommanderInPlay(card.Commander)) return $"You need your {card.Commander} in play to play {card.Name}.";
            if (p.GetEnergy() < card.PowerUpCost) return $"{card.Name} costs {card.PowerUpCost} energy to play.";
            var map = Map.TerritoryRepository.Instance;
            switch (card.CardType)
            {
                case CardType.HiddenEnergy:
                    if (map == null || !map.Territories.Any(t => t.Kind == Map.TerrainKind.Sea))
                        return "There are no seas on this map.";
                    break;
                case CardType.EnergyExtraction:
                    if (map == null || !map.Territories.Any(t => t.Kind == Map.TerrainKind.Moon))
                        return "There is no moon on this map.";
                    break;
                case CardType.AquaBrother:
                    if (map == null || !map.Territories.Any(t => t.Kind == Map.TerrainKind.Sea))
                        return "There are no seas on this map.";
                    break;
                case CardType.NickyBoy:
                    if (map == null || !map.Territories.Any(t => t.Kind == Map.TerrainKind.Moon))
                        return "There is no moon on this map.";
                    break;
                case CardType.InvadeSurface:
                    if (map == null || !p.Territories.Any(t => t.Kind == Map.TerrainKind.Moon && t.Troops > 0))
                        return "You need units on the Moon.";
                    if (map.Territories.All(t => t.Kind != Map.TerrainKind.Land || t.Owner == p))
                        return "You already occupy every land territory.";
                    break;
            }
            switch (card.Timing)
            {
                case CardTiming.BeforeFirstInvasion:
                    if (!IsOwnTurnBeforeFirstInvasion(p)) return $"{card.Name} can only be played on your turn, before your first invasion.";
                    if (!_gm.ShopClosed && !_gm.AllowCardsBeforeShop)
                        return "Finish hiring and buying before you play a command card.";
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
            if (!int.TryParse(option.Substring(CardOptionPrefix.Length), out var id)) return null;
            var held = p.Cards.FirstOrDefault(c => c.Id == id);
            if (held != null) return held;
            var card = _cr.CardById(id);
            return Reveal(p, card) ? card : null;
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
            var ground = Map.Frontiers.CardTerrain(card.CardType);
            if (ground != null && attack.Target.Kind != ground)
                return $"{card.Name} is only played when a {ground.ToString().ToLowerInvariant()} territory is invaded.";
            switch (card.CardType)
            {
                case CardType.StealthStation:
                    if (attack.Target.Kind != Map.TerrainKind.Land) return "A space station is built on land.";
                    if (attack.Target.containsSpaceStation) return $"{attack.Target.Name} already has a space station.";
                    if (Purchases.StationsOf(p) >= Purchases.MaxStations) return $"You already have {Purchases.MaxStations} space stations.";
                    break;
                case CardType.Evacuation:
                    if (p.GetTerritoryCount() <= 1) return "You have nowhere to evacuate to.";
                    break;
            }
            return null;
        }

        // Default Evacuation destination (bots, timeouts): the defender's strongest other territory (no path needed).
        // A human defender picks it (DecisionKind.EvacuationDestination).
        public static Map.Territory EvacuationDestination(Player p, Map.Territory attacked) =>
            p.Territories.Where(t => t != attacked).OrderByDescending(t => t.Troops).FirstOrDefault();

        // Plays a reaction card. Returns true if the invasion is cancelled (Cease Fire).
        public bool PlayReaction(Player p, Card card, Actions.AttackAction attack, Map.Territory evacuateTo = null)
        {
            if (evacuateTo != null && (evacuateTo.Owner != p || evacuateTo == attack.Target))
                throw new InvalidOperationException("Evacuate to another territory you hold.");
            var why = WhyCantReact(p, card, attack);
            if (why != null) throw new InvalidOperationException(why);

            p.SetEnergy(p.GetEnergy() - card.PowerUpCost);
            p.RemoveCard(card);
            _discard.Add(card);
            Debug.Log($"{p.Name} plays {card.Name} against {attack.Player.Name}'s invasion of {attack.Target.Name}.");

            var before = UnitsNow();
            var cancelled = false;
            switch (card.CardType)
            {
                case CardType.StealthModsL:
                case CardType.StealthModsNa:
                case CardType.StealthModsS:
                case CardType.StealthStation:
                    card.UseAbility(attack.Target);
                    break;
                case CardType.LandDeathTrap:
                case CardType.WaterDeathTrap:
                case CardType.OrbitalMines:
                    card.UseAbility(attack.Origin);
                    break;
                case CardType.Evacuation:
                    card.UseAbility(attack.Target, evacuateTo ?? EvacuationDestination(p, attack.Target));
                    break;
                case CardType.CeaseFire:
                    cancelled = true; // AttackPhase stops the invasion and blocks further attacks on p this turn
                    break;
            }
            OnCardPlayed?.Invoke(p, card);
            Report(p, card, new object[] { attack.Target }, before, true);
            return cancelled;
        }

        public Card Buy(Player p, CommanderType deck)
        {
            var why = WhyCantBuy(p, deck);
            if (why != null) throw new InvalidOperationException(why);

            var card = Draw(p, c => c.Commander == deck);
            p.SetEnergy(p.GetEnergy() - BuyCost);
            BuysThisTurn++;
            if (card != null && !card.IsHidden) card.Seal();
            Debug.Log($"{p.Name} bought a {deck} card face-down ({BuysThisTurn}/{MaxBuysPerTurn} this turn).");
            OnCardBought?.Invoke(p, card);
            return card;
        }

        public Action<Player, Card> OnConquestBonus;

        // Free card for the conquest bonus: from a deck whose commander the player has in play if possible,
        // otherwise from any deck in play. null when every deck is empty.
        public Card GrantConquestBonus(Player p)
        {
            p.SetEnergy(p.GetEnergy() + IncomeRules.ConquestBonusEnergy);
            var anyPreferred = DeckLists.DecksInPlay.Any(d => p.HasCommanderInPlay(d) && CardsLeftInDeck(d) > 0);
            var card = Draw(p, c => IsDeckInPlay(c.Commander) && (!anyPreferred || p.HasCommanderInPlay(c.Commander)));
            Debug.Log($"{p.Name} conquered {IncomeRules.ConquestBonusTerritories} territories: +{IncomeRules.ConquestBonusEnergy} energy" +
                      (card != null ? $" and a {card.Commander} card." : " (no cards left to draw)."));
            OnConquestBonus?.Invoke(p, card);
            return card;
        }

        public void Play(Player p, Card card, object[] targets)
        {
            var why = WhyCantPlay(p, card, targets);
            if (why != null) throw new InvalidOperationException(why);

            p.SetEnergy(p.GetEnergy() - card.PowerUpCost);
            Resolve(p, card, targets);
        }

        private void Resolve(Player p, Card card, object[] targets)
        {
            targets ??= Array.Empty<object>();
            p.RemoveCard(card);
            _discard.Add(card); // never reshuffled
            var args = card.Spec.OwnerIsFirstArgument
                ? new object[] { p }.Concat(targets ?? Array.Empty<object>()).ToArray()
                : targets ?? Array.Empty<object>();
            Debug.Log($"{p.Name} played {card.Name} (cost {card.PowerUpCost}).");
            var before = UnitsNow();
            _note = null;
            switch (card.CardType)
            {
                case CardType.HiddenEnergy:
                    PlayHiddenEnergy(p);
                    break;
                case CardType.EnergyExtraction:
                    PlayEnergyExtraction(p);
                    break;
                case CardType.InvadeSurface:
                    PlayInvadeSurface(p);
                    break;
                case CardType.ScoutForces:
                    Scout(p);
                    break;
                case CardType.Redeployment:
                    _gm.FortifyPhase.AddExtraMove();
                    break;
                case CardType.ModReduction:
                    StartModReduction(p);
                    break;
                case CardType.Armageddon:
                    StartArmageddon(p);
                    break;
                case CardType.ReinforcementsL:
                case CardType.ReinforcementsNa:
                case CardType.ReinforcementsS:
                    var padded = new object[] { null, null, null };
                    for (var i = 0; i < targets.Length && i < 3; i++) padded[i] = targets[i];
                    card.UseAbility(padded);
                    break;
                case CardType.DecoyRevealed:
                    for (var i = 0; i + 2 < targets.Length; i += 3)
                        Actions.FortifyAction.MoveCommanders((Map.Territory)targets[i], (Map.Territory)targets[i + 2],
                            new[] { (CommanderType)targets[i + 1] });
                    break;
                default:
                    card.UseAbility(args);
                    break;
            }
            OnCardPlayed?.Invoke(p, card);
            Report(p, card, targets, before, false);
        }

        private bool IsOwnTurnBeforeFirstInvasion(Player p) => _gm.IsCommandStep(p);

        // ---------------- Armageddon ----------------
        // "All players, in turn order, may play any number of nuclear command cards without paying the energy cost."
        // Starting with the player who played it, each player is asked (ArmageddonCard) until they pass. A card with
        // a target asks for it next (ArmageddonTarget). The usual rules still apply apart from energy and timing:
        // the Nuclear Commander must be in play and the player must not be jammed.
        private readonly Queue<Player> _armageddon = new();
        private Card _armageddonCard;
        public Player ArmageddonPlayer { get; private set; }

        public string WhyCantPlayFree(Player p, Card card)
        {
            if (card == null || !p.Cards.Contains(card)) return $"{p.Name} doesn't hold that card.";
            if (card.Commander != CommanderType.NuclearCommander || card.CardType == CardType.Armageddon)
                return "Only other nuclear cards can be played during Armageddon.";
            if (card.Timing == CardTiming.NotImplemented) return $"{card.Name} can't be played yet (not implemented).";
            if (!p.canPlayCommandCards) return "You are jammed (Frequency Jam) and can't play command cards this turn.";
            if (!p.HasCommanderInPlay(card.Commander)) return $"You need your {card.Commander} in play to play {card.Name}.";
            var targets = ArmageddonTargets(p, card);
            if (targets != null && targets.Count == 0) return $"{card.Name} has nothing to target.";
            return null;
        }

        // Territory names the card can target, or null if it takes no target.
        public static List<string> ArmageddonTargets(Player p, Card card) => card.CardType switch
        {
            CardType.RocketStrikeLand or CardType.RocketStrikeWater or CardType.RocketStrikeMoon =>
                Map.TerritoryRepository.Instance.Territories
                .Where(t => t.Owner != p && (Map.Frontiers.CardTerrain(card.CardType) == null || t.Kind == Map.Frontiers.CardTerrain(card.CardType)))
                .Select(t => t.Name).ToList(),
            CardType.AssassinBomb => Map.TerritoryRepository.Instance.Territories
                .Where(t => t.Owner != p && t.CommandersList.Count > 0).Select(t => t.Name).ToList(),
            _ => null,
        };

        // Assassin Bomb goes for the Nuclear Commander when there is one (same choice as the card bar).
        public static CommanderType AssassinTarget(Map.Territory t) =>
            t.CommandersList.OrderByDescending(c => c.CommanderType == CommanderType.NuclearCommander).First().CommanderType;

        private void StartArmageddon(Player owner)
        {
            var order = _gm.GetPlayersInTurnOrder().Where(o => !o.IsDead()).ToList();
            var start = Math.Max(0, order.IndexOf(owner));
            _armageddon.Clear();
            for (var i = 0; i < order.Count; i++)
                _armageddon.Enqueue(order[(start + i) % order.Count]);
            NextArmageddonPlayer();
        }

        private void NextArmageddonPlayer()
        {
            while (_armageddon.Count > 0)
            {
                ArmageddonPlayer = _armageddon.Dequeue();
                if (AskArmageddon()) return;
            }
            ArmageddonPlayer = null;
            Debug.Log("Armageddon is over.");
        }

        private bool AskArmageddon()
        {
            var p = ArmageddonPlayer;
            if (!Secrets.PublicBool(() => p.Cards.Any(c => WhyCantPlayFree(p, c) == null))) return false;
            var known = Secrets.Knows(p);
            var options = known
                ? p.Cards.Where(c => WhyCantPlayFree(p, c) == null).Select(OptionFor).Append(DecisionOptions.Pass).ToList()
                : new List<string> { DecisionOptions.Pass };
            _gm.OpenDecision(new PendingDecision(_gm.GetPlayerId(p), DecisionKind.ArmageddonCard, options, anyCard: !known),
                OnArmageddonCard);
            return true;
        }

        private void OnArmageddonCard(Actions.DecisionResponseAction response)
        {
            var p = response.Player;
            var card = CardFromOption(p, response.Option);
            if (card == null)
            {
                NextArmageddonPlayer();
                return;
            }
            var targets = ArmageddonTargets(p, card);
            if (targets == null)
            {
                PlayFree(p, card, Array.Empty<object>());
                return;
            }
            _armageddonCard = card;
            _gm.OpenDecision(new PendingDecision(_gm.GetPlayerId(p), DecisionKind.ArmageddonTarget, targets, card.Name),
                OnArmageddonTarget);
        }

        private void OnArmageddonTarget(Actions.DecisionResponseAction response)
        {
            var card = _armageddonCard;
            _armageddonCard = null;
            var t = Map.TerritoryRepository.Instance.FromName(response.Option);
            var targets = card.CardType == CardType.AssassinBomb ? new object[] { t, AssassinTarget(t) } : new object[] { t };
            PlayFree(response.Player, card, targets);
        }

        private void PlayFree(Player p, Card card, object[] targets)
        {
            var why = WhyCantPlayFree(p, card) ?? card.Spec.CheckTargets?.Invoke(p, targets);
            if (why != null) Debug.LogWarning($"Armageddon: {p.Name} can't play {card.Name}: {why}");
            else
            {
                Debug.Log($"Armageddon: {p.Name} plays {card.Name} for free.");
                Resolve(p, card, targets);
            }
            if (!AskArmageddon()) NextArmageddonPlayer();
        }
    }
}
