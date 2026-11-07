using System;
using System.Linq;

namespace InvadeEarth.Rules
{
    public sealed class BoardTerritory
    {
        public string Name;
        public int OwnerId;
        public int Troops;
        public bool Station;
        public int[] Neighbours = Array.Empty<int>();
    }

    // A map with no scene. Reinforce, attack, move-in and one fortify, using the same
    // income, movement and battle rules as the Unity game.
    public sealed class MatchBoard
    {
        public BoardTerritory[] Territories = Array.Empty<BoardTerritory>();
        public int[] Energy = Array.Empty<int>();
        public int ActivePlayer = -1;
        public int RemainingToPlace;
        public string Phase = "";
        public BattleOutcome LastBattle;
        public bool LastConquered;
        public bool DefenderEliminated;
        public int LastFrom = -1;
        public int LastTo = -1;

        public int CountOf(int playerId) => Territories.Count(t => t.OwnerId == playerId);

        public void BeginReinforce(int playerId, int continentBonus)
        {
            ActivePlayer = playerId;
            Energy[playerId] += IncomeRules.EnergyIncome(CountOf(playerId), continentBonus);
            RemainingToPlace = IncomeRules.TroopBonus(CountOf(playerId), continentBonus);
            foreach (var territory in Territories)
                if (territory.OwnerId == playerId && territory.Station)
                    territory.Troops += IncomeRules.StationTroopGrant;
            Phase = RemainingToPlace == 0 ? "Attack" : "Reinforce";
        }

        public bool Place(int territory, int amount)
        {
            if (Phase != "Reinforce") return false;
            if (territory < 0 || territory >= Territories.Length) return false;
            if (Territories[territory].OwnerId != ActivePlayer) return false;
            if (!TroopMovement.TryPlace(Territories[territory].Troops, amount, RemainingToPlace, out var troops, out var left))
                return false;
            Territories[territory].Troops = troops;
            RemainingToPlace = left;
            if (RemainingToPlace == 0) Phase = "Attack";
            return true;
        }

        public void EndReinforce(IRandom random)
        {
            if (Phase != "Reinforce") return;
            var mine = Enumerable.Range(0, Territories.Length).Where(i => Territories[i].OwnerId == ActivePlayer).ToArray();
            foreach (var pick in TroopMovement.ScatterPicks(mine.Length, RemainingToPlace, random))
                Territories[mine[pick]].Troops++;
            RemainingToPlace = 0;
            Phase = "Attack";
        }

        // mods are d6. attackD8 and defenseD8 are commander / station dice already decided by the caller.
        public bool Attack(int from, int to, int mods, int attackD8, int defenseD8, IRandom random)
        {
            if (Phase != "Attack") return false;
            if (!CanAttack(from, to, mods)) return false;
            var origin = Territories[from];
            var target = Territories[to];
            var attackDice = Math.Min(BattleRules.MaxAttackDice, mods + attackD8);
            var defenseDice = Math.Min(BattleRules.MaxDefenseDice, target.Troops);
            var outcome = BattleRules.Resolve(new BattleRequest
            {
                AttackMods = mods,
                AttackD8 = Math.Min(attackD8, attackDice),
                AttackD6 = attackDice - Math.Min(attackD8, attackDice),
                DefenseD8 = Math.Min(defenseD8, defenseDice),
                DefenseD6 = defenseDice - Math.Min(defenseD8, defenseDice),
                OriginTroops = origin.Troops,
                TargetTroops = target.Troops,
            }, random);
            var applied = TroopMovement.ApplyBattle(origin.Troops, target.Troops, outcome.AttackerLosses, outcome.CommanderLosses, outcome.DefenderLosses, outcome.RemainingDefendingTroops);
            origin.Troops = applied.OriginTroops;
            target.Troops = applied.TargetTroops;
            LastBattle = outcome;
            LastConquered = applied.Conquered;
            LastFrom = from;
            LastTo = to;
            DefenderEliminated = false;
            if (applied.Conquered)
            {
                var defender = target.OwnerId;
                target.OwnerId = ActivePlayer;
                DefenderEliminated = Territories.All(t => t.OwnerId != defender);
                Phase = "MoveIn";
            }
            return true;
        }

        int _attackMods;

        public bool MoveIn(int amount)
        {
            if (Phase != "MoveIn") return false;
            var min = TroopMovement.MinMoveIn(_attackMods, Territories[LastFrom].Troops);
            if (amount < min) return false;
            var moved = TroopMovement.Move(Territories[LastFrom].Troops, Territories[LastTo].Troops, amount);
            if (!moved.Ok) return false;
            Territories[LastFrom].Troops = moved.FromTroops;
            Territories[LastTo].Troops = moved.ToTroops;
            Phase = "Attack";
            return true;
        }

        public void EndAttack()
        {
            if (Phase == "Attack") Phase = "Fortify";
        }

        public bool Fortify(int from, int to, int amount)
        {
            if (Phase != "Fortify") return false;
            if (!AdjacentSameOwner(from, to)) return false;
            var moved = TroopMovement.Move(Territories[from].Troops, Territories[to].Troops, amount);
            if (!moved.Ok) return false;
            Territories[from].Troops = moved.FromTroops;
            Territories[to].Troops = moved.ToTroops;
            Phase = "Done";
            return true;
        }

        bool CanAttack(int from, int to, int mods)
        {
            if (from < 0 || to < 0 || from >= Territories.Length || to >= Territories.Length) return false;
            var origin = Territories[from];
            var target = Territories[to];
            if (origin.OwnerId != ActivePlayer || target.OwnerId == ActivePlayer) return false;
            if (!origin.Neighbours.Contains(to)) return false;
            if (mods < 1 || mods > BattleRules.MaxAttackDice || mods > TroopMovement.Available(origin.Troops)) return false;
            _attackMods = mods;
            return true;
        }

        bool AdjacentSameOwner(int from, int to)
        {
            if (from < 0 || to < 0 || from >= Territories.Length || to >= Territories.Length) return false;
            return Territories[from].OwnerId == ActivePlayer
                   && Territories[to].OwnerId == ActivePlayer
                   && Territories[from].Neighbours.Contains(to);
        }
    }
}
