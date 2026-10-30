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
        var attackingTroops = Math.Min(attackAction.Troops, MaxAttackDice);
        var defendingTroops = Math.Min(attackAction.Target.Troops, MaxDefenseDice); // rules: at most 2 (was 3)

        var battleWidth = Math.Min(attackingTroops, defendingTroops);
        // 2210 dice: each attacking commander with an attack bonus replaces a d6 with a d8
        // (Land Commander on land = always on our map; Nuclear always; Diplomat never).
        // Defending: every unit rolls d8 in a territory with a space station; otherwise each commander there rolls a d8.
        var (attackD8, attackD6) = AttackDice(attackAction.Origin, attackingTroops);
        var (defenseD8, defenseD6) = DefenseDice(attackAction.Target, defendingTroops);
        var attackerRolls = Roll8SidedDies(attackD8).Concat(Roll6SidedDies(attackD6)).ToArray();
        var defenderRolls = Roll8SidedDies(defenseD8).Concat(Roll6SidedDies(defenseD6)).ToArray();

        attackerRolls = attackerRolls.OrderByDescending(c => c).ToArray();
        defenderRolls = defenderRolls.OrderByDescending(c => c).ToArray();

        var attackerLosses = 0;
        var defenderLosses = 0;

        for (int i = 0; i < battleWidth; i++)
        {
            if (attackerRolls[i] > defenderRolls[i])
                defenderLosses++;
            else
                attackerLosses++;
        }

        var remainingAttackingTroops = attackAction.Origin.Troops - attackerLosses;
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
        );
    }

    public const int MaxAttackDice = 3;
    public const int MaxDefenseDice = 2;

    public static bool AttacksWithD8(CommanderType type) =>
        type == CommanderType.LandCommander || type == CommanderType.NuclearCommander;

    public static (int d8, int d6) AttackDice(Territory origin, int dice)
    {
        var bonus = origin.CommandersList.Count(c => AttacksWithD8(c.CommanderType));
        var d8 = Math.Min(dice, bonus);
        return (d8, dice - d8);
    }

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