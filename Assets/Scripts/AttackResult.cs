using System;
using Actions;
using Map;

public class AttackResult
{
    public AttackAction AttackAction { get; }

    public int AttackerLosses { get; }
    public int DefenderLosses { get; }

    /// <summary>
    /// all troops left in the attacking territory
    /// </summary>
    public int RemainingAttackingTroops { get; }

    public int RemainingAvailableAttackingTroops => RemainingAttackingTroops - 1;
    public int RemainingDefendingTroops { get; }

    public Territory Origin => AttackAction.Origin;
    public Territory Target => AttackAction.Target;
    public int AttackingTroops => AttackAction.Troops;
    public int DefendingTroops { get; }
    
    public int[] AttackerRolls { get; }
    public int[] DefenderRolls { get; }
    // Sides of each die, in the same order as the rolls (8 = commander / space station die, 6 = normal).
    public int[] AttackerDieSides { get; set; } = Array.Empty<int>();
    public int[] DefenderDieSides { get; set; } = Array.Empty<int>();
    public int AttackerD8Count => Array.FindAll(AttackerDieSides, s => s == 8).Length;
    public int DefenderD8Count => Array.FindAll(DefenderDieSides, s => s == 8).Length;


public AttackResult(
        AttackAction attackAction,
        int attackerLosses,
        int defenderLosses,
        int remainingAttackingTroops,
        int remainingDefendingTroops,
        int defendingTroops,
        int[] attackerRolls,
        int[] defenderRolls
    )
    {
        AttackAction = attackAction;
        AttackerLosses = attackerLosses;
        DefenderLosses = defenderLosses;
        RemainingAttackingTroops = remainingAttackingTroops;
        RemainingDefendingTroops = remainingDefendingTroops;
        DefendingTroops = defendingTroops;
        AttackerRolls = attackerRolls;
        DefenderRolls = defenderRolls;
    }

    public bool HasAttackerWonTerritory() => RemainingDefendingTroops == 0;

    public int GetMinTroopsToMoveAfterWin() => Math.Min(Math.Min(AttackingTroops, 3), RemainingAvailableAttackingTroops);
}