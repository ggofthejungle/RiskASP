using System;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Expansions;
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


    public AttackResult SimulateAttack(AttackAction attackAction)
    {
        var attackingTroops = attackAction.Dice; // units (MODs + commanders), max 3
        var defendingTroops = Math.Min(attackAction.Target.Troops, MaxDefenseDice); // rules: at most 2 (was 3)

        var battleWidth = Math.Min(attackingTroops, defendingTroops);
        // 2210 dice: each attacking commander with an attack bonus replaces a d6 with a d8
        // (Land Commander on land = always on our map; Nuclear always; Diplomat never).
        // Defending: every unit rolls d8 in a territory with a space station; otherwise each commander there rolls a d8.
        var (attackD8, attackD6) = AttackDice(attackAction.Commanders, attackingTroops);
        var (defenseD8, defenseD6) = DefenseDice(attackAction.Target, defendingTroops);
        // Keep each roll with its die (8 or 6 sides), sorted highest first, so the UI can show which were d8.
        var attackerDice = Roll8SidedDies(attackD8).Select(v => (v, sides: 8))
            .Concat(Roll6SidedDies(attackD6).Select(v => (v, sides: 6)))
            .OrderByDescending(d => d.v).ThenByDescending(d => d.sides).ToArray();
        var defenderDice = Roll8SidedDies(defenseD8).Select(v => (v, sides: 8))
            .Concat(Roll6SidedDies(defenseD6).Select(v => (v, sides: 6)))
            .OrderByDescending(d => d.v).ThenByDescending(d => d.sides).ToArray();
        var attackerRolls = attackerDice.Select(d => d.v).ToArray();
        var defenderRolls = defenderDice.Select(d => d.v).ToArray();
        if (attackD8 + defenseD8 > 0)
            Debug.Log($"Dice: attacker {attackD8} x d8 + {attackD6} x d6, defender {defenseD8} x d8 + {defenseD6} x d6");

        var attackerLosses = 0;
        var defenderLosses = 0;

        for (int i = 0; i < battleWidth; i++)
        {
            if (attackerRolls[i] > defenderRolls[i])
                defenderLosses++;
            else
                attackerLosses++;
        }

        // Losses come off the attacking MODs first; a commander only dies when no attacking MOD is left to take the hit.
        var modLosses = Math.Min(attackerLosses, attackAction.Troops);
        var commanderLosses = attackerLosses - modLosses;
        var remainingAttackingTroops = attackAction.Origin.Troops - modLosses;
        var remainingDefendingTroops = attackAction.Target.Troops - defenderLosses;
        
        return new AttackResult(
            attackAction,
            attackerLosses,
            defenderLosses,
            remainingAttackingTroops,
            remainingDefendingTroops,
            defendingTroops,
            attackerRolls,
            defenderRolls
        )
        {
            AttackerDieSides = attackerDice.Select(d => d.sides).ToArray(),
            AttackerCommanderLosses = commanderLosses,
            DefenderDieSides = defenderDice.Select(d => d.sides).ToArray(),
        };
    }

    public const int MaxAttackDice = 3;
    public const int MaxDefenseDice = 2;

    public static bool AttacksWithD8(CommanderType type) =>
        type == CommanderType.LandCommander || type == CommanderType.NuclearCommander;

    // Dice for the attacking commanders that join (Land/Nuclear roll d8) plus MODs (d6).
    public static (int d8, int d6) AttackDice(IEnumerable<CommanderType> attackingCommanders, int dice)
    {
        var bonus = attackingCommanders.Count(AttacksWithD8);
        var d8 = Math.Min(dice, bonus);
        return (d8, dice - d8);
    }

    // As if every d8 commander in the territory joined.
    public static (int d8, int d6) AttackDice(Territory origin, int dice) =>
        AttackDice(origin.CommandersList.Select(c => c.CommanderType), dice);

    public static (int d8, int d6) DefenseDice(Territory target, int dice)
    {
        if (target.containsSpaceStation) return (dice, 0);
        var d8 = Math.Min(dice, target.CommandersList.Count);
        return (d8, dice - d8);
    }

    //roll 6 sided die (1 thru 6)
    private int SixSidedRoll() => UnityEngine.Random.Range(1, 7);
    
    //roll 8 sided die (1 thru 8)
    private int EightSidedRoll() => UnityEngine.Random.Range(1, 9); 

    private int[] Roll6SidedDies(int dices)
    {
        // var rolls = new List<int>(dices);
        var rolls = new int[dices];

        for (int i = 0; i < dices; i++) 
            rolls[i] = SixSidedRoll();

        return rolls;
    }
    
    //Some Commanders and Space Stations use 8 sided dies
    private int[] Roll8SidedDies(int dices)
    {
        // var rolls = new List<int>(dices);
        var rolls = new int[dices];

        for (int i = 0; i < dices; i++) 
            rolls[i] = EightSidedRoll();
        
        //can we access _diceSprites in here and change it from 6 to 8? 
        //Debug.Log("BattleSimulator " + _diceSprites.Length);
        

        return rolls;
    }
}