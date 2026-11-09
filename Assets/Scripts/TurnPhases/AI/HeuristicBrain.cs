using System.Collections.Generic;
using System.Linq;
using Actions;
using InvadeEarth.Rules;
using Map;
using player;

namespace TurnPhases.AI
{
    // MM-2: the bot's MODs without the ASP solver. Builds a plain board from the scene, asks BotTactics,
    // and queues the answer as normal actions (online they go through the relay like any other bot move).
    // Called once per step: at the start of each phase and after every attack, like the solver.
    public class HeuristicBrain
    {
        private readonly GameManager _gm;
        private readonly ActionReader _ar;

        public HeuristicBrain(GameManager gm, ActionReader ar)
        {
            _gm = gm;
            _ar = ar;
        }

        public void Act(Player player)
        {
            if (_gm == null || player == null || !_gm.IsCurrentPlayer(player) || _gm.PendingDecision != null) return;
            switch (_gm.CurrentPhase)
            {
                case ReinforcePhase rp: Reinforce(player, rp); break;
                case AttackPhase ap: Attack(player, ap); break;
                case FortifyPhase: Fortify(player); break;
            }
        }

        void Reinforce(Player player, ReinforcePhase phase)
        {
            var board = Build(out var territories, out var bonus);
            var me = _gm.GetPlayerId(player);
            var placements = BotTactics.Place(board, me, phase.RemainingTroopsToPlace, bonus);
            foreach (var p in placements)
                _ar.AddAction(new ReinforceAction(player, _gm.Turn, territories[p.Territory], p.Amount));
            if (placements.Count == 0)
                _ar.AddAction(new EndPhaseAction(player, _gm.Turn));
        }

        void Attack(Player player, AttackPhase phase)
        {
            var board = Build(out var territories, out var bonus, player);
            var me = _gm.GetPlayerId(player);
            if (phase.State == AttackState.Fortifying)
            {
                var won = phase.LastAttackResult.AttackAction;
                var min = AttackReinforceAction.MinToMove(won, phase.LastAttackResult.DefendingTroops > 0);
                var amount = BotTactics.MoveIn(board, territories.IndexOf(won.Origin), territories.IndexOf(won.Target), min);
                _ar.AddAction(new AttackReinforceAction(player, _gm.Turn, phase.AttackTurn, won, amount));
                return;
            }
            if (phase.State != AttackState.Attacking) return;

            var blocked = _gm.Players.Where(phase.IsCeaseFireAgainst).Select(_gm.GetPlayerId).ToList();
            var earth = new List<int>();
            if (_gm.CommandCards != null)
                foreach (var land in _gm.CommandCards.SurfaceLands(player))
                {
                    var at = territories.IndexOf(land);
                    if (at >= 0) earth.Add(at);
                }
            var pick = BotTactics.ChooseAttack(board, me, bonus, blocked,
                player.HasCommanderInPlay(Expansions.CommanderType.NavalCommander),
                player.HasCommanderInPlay(Expansions.CommanderType.SpaceCommander),
                earth);
            if (pick == null)
            {
                _ar.AddAction(new EndPhaseAction(player, _gm.Turn));
                return;
            }
            var a = pick.Value;
            var from = territories[a.From];
            var commanders = AttackAction.D8Commanders(from, territories[a.To])
                .Take(System.Math.Max(0, BattleSimulator.MaxAttackDice - a.Mods));
            _ar.AddAction(new AttackAction(player, _gm.Turn, phase.AttackTurn, from, territories[a.To], a.Mods, commanders));
        }

        void Fortify(Player player)
        {
            var board = Build(out var territories, out _);
            var move = BotTactics.Fortify(board, _gm.GetPlayerId(player));
            if (move == null)
            {
                _ar.AddAction(new EndPhaseAction(player, _gm.Turn));
                return;
            }
            var m = move.Value;
            var redeploy = Redeployment(player, board, m);
            if (redeploy != null)
                _ar.AddAction(new PlayCommandCardAction(player, _gm.Turn, redeploy, new object[0]));
            var from = territories[m.From];
            // Commanders stay where they are: a lone commander on the border dies to the first attack.
            _ar.AddAction(new FortifyAction(player, _gm.Turn, from, territories[m.To], m.Amount));
        }

        // Redeployment (one extra move) when a second move is still worth making after this one.
        Cards.Card Redeployment(Player player, BoardTerritory[] board, BotMove first)
        {
            if (_gm.FortifyPhase.ExtraMoves > 0) return null;
            var card = player.Cards.FirstOrDefault(c => c.CardType == Cards.CardType.Redeployment && !c.IsHidden
                                                        && _gm.CommandCards.WhyCantPlay(player, c, new object[0]) == null);
            if (card == null) return null;
            board[first.From].Troops -= first.Amount;
            board[first.To].Troops += first.Amount;
            return BotTactics.Fortify(board, _gm.GetPlayerId(player)) != null ? card : null;
        }

        BoardTerritory[] Build(out List<Territory> territories, out List<int> continentBonus, Player surfaceFor = null)
        {
            territories = TerritoryRepository.Instance.Territories.ToList();
            var index = new Dictionary<Territory, int>();
            for (var i = 0; i < territories.Count; i++) index[territories[i]] = i;

            var continents = ContinentRepository.Instance != null
                ? ContinentRepository.Instance.Continents.Values.OrderBy(c => c.Name).ToList()
                : new List<Continent>();
            continentBonus = continents.Select(c => c.BonusTroops).ToList();
            var continentOf = new Dictionary<Territory, int>();
            for (var c = 0; c < continents.Count; c++)
                foreach (var t in continents[c].Territories)
                    continentOf[t] = c;

            var board = new BoardTerritory[territories.Count];
            for (var i = 0; i < territories.Count; i++)
            {
                var t = territories[i];
                board[i] = new BoardTerritory
                {
                    Name = t.Name,
                    OwnerId = t.Owner != null ? _gm.GetPlayerId(t.Owner) : -1,
                    Troops = t.Troops,
                    Station = t.containsSpaceStation,
                    Terrain = t.Kind.ToString(),
                    Neighbours = t.NeighbourTerritories.Where(index.ContainsKey).Select(n => index[n]).ToArray(),
                    Continent = continentOf.TryGetValue(t, out var c) ? c : -1,
                };
            }
            if (surfaceFor != null && _gm.CommandCards != null)
            {
                foreach (var land in _gm.CommandCards.SurfaceLands(surfaceFor))
                {
                    if (!index.TryGetValue(land, out var to)) continue;
                    for (var i = 0; i < territories.Count; i++)
                    {
                        if (!_gm.CommandCards.AllowsSurfaceAttack(surfaceFor, territories[i], land)) continue;
                        var n = board[i].Neighbours;
                        if (System.Array.IndexOf(n, to) >= 0) continue;
                        var grown = new int[n.Length + 1];
                        System.Array.Copy(n, grown, n.Length);
                        grown[n.Length] = to;
                        board[i].Neighbours = grown;
                    }
                }
            }
            return board;
        }
    }
}
