using System;
using System.Collections.Generic;
using System.Linq;
using Expansions;
using Map;
using player;
using TurnPhases;

namespace Actions
{
    public class AttackAction : AttackPhaseAction
    {
        public Territory Origin { get; }
        public Territory Target { get; }
        public int Troops { get; } // attacking units (MODs), 0..3
        // Commanders joining the attack (each is a unit with its own die: d8 for Land/Nuclear, d6 for Diplomat).
        // Not given = no commanders join (the player is asked in the UI; bots name theirs explicitly).
        public IReadOnlyList<CommanderType> Commanders { get; }
        public int Dice => Math.Min(BattleSimulator.MaxAttackDice, Troops + Commanders.Count);

        public AttackAction(Player player, int turn, int attackTurn, Territory origin, Territory target, int troops,
            IEnumerable<CommanderType> commanders = null) :
            base(player, turn, attackTurn)
        {
            Origin = origin;
            Target = target;
            Troops = troops;
            Commanders = (commanders ?? Enumerable.Empty<CommanderType>()).ToList();
        }

        // The commanders in a territory that attack with an 8-sided die (what the AI brings along).
        public static List<CommanderType> D8Commanders(Territory origin, Territory target) =>
            origin.CommandersList.Where(c => BattleSimulator.AttacksWithD8(c.CommanderType, origin, target)).Select(c => c.CommanderType).ToList();

        public override bool IsValid()
        {
            var gm = GameManager.Instance;
            if(gm.AttackPhase.State != AttackState.Attacking)
            {
                LogError($"AttackPhase is not in Attacking state");
                return false;
            }
            
            if (gm.AttackPhase.IsCeaseFireAgainst(Target.Owner))
            {
                UnityEngine.Debug.LogWarning($"AttackAction REJECTED: Cease Fire - {Player.Name} can't attack {Target.Owner.Name} this turn");
                return false;
            }

            if (Troops > BattleSimulator.MaxAttackDice)
            {
                LogError($"Troops ({Troops}) is greater than {BattleSimulator.MaxAttackDice}");
                return false;
            }

            if (Troops + Commanders.Count > BattleSimulator.MaxAttackDice)
            {
                LogError($"MODs ({Troops}) + commanders ({Commanders.Count}) exceed {BattleSimulator.MaxAttackDice} dice");
                return false;
            }

            foreach (var group in Commanders.GroupBy(c => c))
                if (Origin.CommandersList.Count(c => c.CommanderType == group.Key) < group.Count())
                {
                    LogWarning($"There is no {group.Key} in {Origin.Name} to attack with");
                    return false;
                }

            var units = Origin.Troops + Origin.CommandersList.Count;
            if (units < 2)
            {
                LogWarning($"{Origin.Name} needs at least two units to invade. A commander counts as a unit.");
                return false;
            }

            if (Troops + Commanders.Count > units - 1)
            {
                LogWarning("Leave at least one unit behind.");
                return false;
            }

            if (Troops > Origin.Troops)
            {
                LogError($"Troops ({Troops}) is greater than available troops ({Origin.GetAvailableTroops()})");
                return false;
            }

            if (Troops < 0 || Troops + Commanders.Count < 1)
            {
                LogError($"Nothing attacks (troops {Troops}, commanders {Commanders.Count})");
                return false;
            }

            if (Origin.Owner != Player)
            {
                LogError(
                    $"Origin territory ({Origin.Name} owned by {Origin.Owner.Name}) is not owned by the player ({Player.Name})");
                return false;
            }

            if (Target.Owner == Player)
            {
                LogError($"Target territory ({Target.Name} is a friendly territory)");
                return false;
            }

            var surface = GameManager.Instance != null && GameManager.Instance.CommandCards != null
                && GameManager.Instance.CommandCards.AllowsSurfaceAttack(Player, Origin, Target);
            if (!Origin.IsNeighbourOf(Target) && !surface)
            {
                LogError($"Origin territory ({Origin.Name}) is not a neighbour of Target territory ({Target.Name})");
                return false;
            }

            var locked = Expansions.Purchases.WhyCantEnterBattle(Player, Origin, Target);
            if (locked != null)
            {
                LogWarning(locked);
                return false;
            }

            if (Origin.Kind == TerrainKind.Moon && Target.Kind == TerrainKind.Land && !surface)
            {
                LogWarning("The Moon can attack Earth only with Invade Earth.");
                return false;
            }

            foreach (var type in Commanders)
                if (!Expansions.Purchases.CanStand(type, Target))
                {
                    LogError($"A {type} can't enter {Target.Name}.");
                    return false;
                }

            return base.IsValid();
        }

    }
}