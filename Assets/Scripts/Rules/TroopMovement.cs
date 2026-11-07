using System;

namespace InvadeEarth.Rules
{
    public struct MoveResult
    {
        public bool Ok;
        public int FromTroops;
        public int ToTroops;
    }

    public struct LossResult
    {
        public int OriginTroops;
        public int TargetTroops;
        public int ModLosses;
        public bool Conquered;
    }

    // Troop counts only. One unit must stay behind, except when a territory is conquered
    // (it is left empty and the attacker takes it).
    public static class TroopMovement
    {
        // Can be negative when a territory is empty. Callers that move units treat that as "none available".
        public static int Available(int troops) => troops - 1;

        public static bool TryPlace(int onTerritory, int amount, int remaining, out int newTroops, out int newRemaining)
        {
            newTroops = onTerritory;
            newRemaining = remaining;
            if (amount < 1 || amount > remaining) return false;
            newTroops = onTerritory + amount;
            newRemaining = remaining - amount;
            return true;
        }

        public static MoveResult Move(int fromTroops, int toTroops, int amount)
        {
            if (amount < 1 || amount > Available(fromTroops))
                return new MoveResult { Ok = false, FromTroops = fromTroops, ToTroops = toTroops };
            return new MoveResult { Ok = true, FromTroops = fromTroops - amount, ToTroops = toTroops + amount };
        }

        // After a conquest, at least one of the attacking MODs moves in, and at most the units that can leave.
        public static int MinMoveIn(int attackingMods, int originTroops) =>
            Math.Min(Math.Max(1, Math.Min(attackingMods, BattleRules.MaxAttackDice)), Available(originTroops));

        public static LossResult ApplyBattle(int originTroops, int targetTroops, int attackerLosses, int commanderLosses, int defenderLosses, int remainingDefending)
        {
            var modLosses = attackerLosses - commanderLosses;
            if (modLosses < 0) modLosses = 0;
            var origin = originTroops - modLosses;
            if (origin < 0) origin = 0;
            var target = targetTroops - defenderLosses;
            if (target < 0) target = 0;
            var conquered = remainingDefending == 0;
            if (conquered) target = 0;
            return new LossResult
            {
                OriginTroops = origin,
                TargetTroops = target,
                ModLosses = modLosses,
                Conquered = conquered,
            };
        }

        // One troop at a time onto a uniform pick. `count` is how many territories the player owns.
        public static int[] ScatterPicks(int count, int troops, IRandom random)
        {
            if (count < 1 || troops < 1) return Array.Empty<int>();
            var picks = new int[troops];
            for (var i = 0; i < picks.Length; i++)
                picks[i] = random.Range(0, count);
            return picks;
        }
    }
}
