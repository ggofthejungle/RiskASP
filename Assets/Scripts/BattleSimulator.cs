using System;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Expansions;
using InvadeEarth.Rules;
using Map;
using UnityEngine;

public class BattleSimulator : MonoBehaviour
{
    public static BattleSimulator Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("There is more than one BattleSimulator in the scene");
            Destroy(gameObject);
        }
        else
            Instance = this;
    }


    public AttackResult SimulateAttack(AttackAction attackAction, int defenderDice)
    {
        var attackingTroops = attackAction.Dice; // units (MODs + commanders), max 3
        var defendingTroops = Math.Min(Math.Max(0, defenderDice), Math.Min(attackAction.Target.Troops, MaxDefenseDice));

        // 2210 dice: each attacking commander with an attack bonus replaces a d6 with a d8
        // (Land Commander on land = always on our map; Nuclear always; Diplomat never).
        // Defending: every unit rolls d8 in a territory with a space station; otherwise each commander there rolls a d8.
        var (attackD8, attackD6) = AttackDice(attackAction.Commanders, attackingTroops, attackAction.Origin, attackAction.Target);
        var (defenseD8, defenseD6) = DefenseDice(attackAction.Target, defendingTroops);
        if (attackD8 + defenseD8 > 0)
            Debug.Log($"Dice: attacker {attackD8} x d8 + {attackD6} x d6, defender {defenseD8} x d8 + {defenseD6} x d6");

        var outcome = BattleRules.Resolve(new BattleRequest
        {
            AttackMods = attackAction.Troops,
            AttackD8 = attackD8,
            AttackD6 = attackD6,
            DefenseD8 = defenseD8,
            DefenseD6 = defenseD6,
            OriginTroops = attackAction.Origin.Troops,
            TargetTroops = attackAction.Target.Troops,
        }, GameRandom.Current);

        return new AttackResult(
            attackAction,
            outcome.AttackerLosses,
            outcome.DefenderLosses,
            outcome.RemainingAttackingTroops,
            outcome.RemainingDefendingTroops,
            defendingTroops,
            outcome.AttackerRolls,
            outcome.DefenderRolls
        )
        {
            AttackerDieSides = outcome.AttackerDieSides,
            AttackerCommanderLosses = outcome.CommanderLosses,
            DefenderDieSides = outcome.DefenderDieSides,
        };
    }

    public const int MaxAttackDice = BattleRules.MaxAttackDice;
    public const int MaxDefenseDice = BattleRules.MaxDefenseDice;

    public static bool AttacksWithD8(CommanderType type, Territory from, Territory to) =>
        DiceRules.AttacksWithD8(type.ToString(), from != null ? from.Kind.ToString() : null, to != null ? to.Kind.ToString() : null);

    // Dice for the commanders who join this battle. MODs are the d6s.
    public static (int d8, int d6) AttackDice(IEnumerable<CommanderType> attackingCommanders, int dice, Territory from, Territory to) =>
        DiceRules.Split(dice, attackingCommanders.Count(t => AttacksWithD8(t, from, to)));

    // As if every commander in the territory joined an attack from it. Pass the target once it is known.
    public static (int d8, int d6) AttackDice(Territory origin, int dice, Territory target = null) =>
        AttackDice(origin.CommandersList.Select(c => c.CommanderType), dice, origin, target);

    public static (int d8, int d6) DefenseDice(Territory target, int dice) =>
        DiceRules.DefenseDice(dice, target.containsSpaceStation, target.CommandersList.Count);

}