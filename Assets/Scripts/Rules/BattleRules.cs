using System;
using System.Linq;

namespace InvadeEarth.Rules
{
    public struct BattleRequest
    {
        public int AttackMods;
        public int AttackD8;
        public int AttackD6;
        public int DefenseD8;
        public int DefenseD6;
        public int OriginTroops;
        public int TargetTroops;
    }

    public struct BattleOutcome
    {
        public int AttackerLosses;
        public int DefenderLosses;
        public int CommanderLosses;
        public int RemainingAttackingTroops;
        public int RemainingDefendingTroops;
        public int[] AttackerRolls;
        public int[] DefenderRolls;
        public int[] AttackerDieSides;
        public int[] DefenderDieSides;
    }

    // Dice and losses, with no scene objects. Ties go to the defender.
    // Attacking losses hit MODs first; a commander dies only when no attacking MOD is left.
    public static class BattleRules
    {
        public const int MaxAttackDice = 3;
        public const int MaxDefenseDice = 2;
        public static BattleOutcome Resolve(BattleRequest request, IRandom random)
        {
            var attackDice = Roll(request.AttackD8, 8, random)
                .Concat(Roll(request.AttackD6, 6, random))
                .OrderByDescending(d => d.value)
                .ThenByDescending(d => d.sides)
                .ToArray();
            var defenseDice = Roll(request.DefenseD8, 8, random)
                .Concat(Roll(request.DefenseD6, 6, random))
                .OrderByDescending(d => d.value)
                .ThenByDescending(d => d.sides)
                .ToArray();

            var width = Math.Min(attackDice.Length, defenseDice.Length);
            var attackerLosses = 0;
            var defenderLosses = 0;
            for (var i = 0; i < width; i++)
            {
                if (attackDice[i].value > defenseDice[i].value) defenderLosses++;
                else attackerLosses++;
            }

            var modLosses = Math.Min(attackerLosses, request.AttackMods);
            return new BattleOutcome
            {
                AttackerLosses = attackerLosses,
                DefenderLosses = defenderLosses,
                CommanderLosses = attackerLosses - modLosses,
                RemainingAttackingTroops = request.OriginTroops - modLosses,
                RemainingDefendingTroops = request.TargetTroops - defenderLosses,
                AttackerRolls = attackDice.Select(d => d.value).ToArray(),
                DefenderRolls = defenseDice.Select(d => d.value).ToArray(),
                AttackerDieSides = attackDice.Select(d => d.sides).ToArray(),
                DefenderDieSides = defenseDice.Select(d => d.sides).ToArray(),
            };
        }

        static (int value, int sides)[] Roll(int count, int sides, IRandom random)
        {
            var dice = new (int, int)[count];
            for (var i = 0; i < count; i++)
                dice[i] = (random.Range(1, sides + 1), sides);
            return dice;
        }
    }
}
