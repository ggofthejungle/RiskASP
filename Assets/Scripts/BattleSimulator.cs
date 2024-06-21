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
        var attackingTroops = Math.Min(attackAction.Troops, 3);
        var defendingTroops = Math.Min(attackAction.Target.Troops, 3);

        var battleWidth = Math.Min(attackingTroops, defendingTroops);
        int[] attackerRolls = null;
        

        if (attackAction.Origin.CommandersList.Count == 0)
        {
            attackerRolls = RollDices(attackingTroops);
        }
        else
        {
            Debug.Log("BattleSimulator- attacking with 8 sided die");
            //To do
            //Check to see if we are not attacking water to water and space to space
            //Need logic for space and water commanders
            Debug.Log("BattleSimulator test " + attackAction.Origin.CommandersList.Count);
            for (int i = 0; i < attackAction.Origin.CommandersList.Count; i++)
            {
                if (attackAction.Origin.CommandersList[i].Type == CommanderType.LandCommander)
                {
                    attackerRolls = RollCommanderDices(attackingTroops);
                }else if (attackAction.Origin.CommandersList[i].Type == CommanderType.NuclearCommander) 
                    attackerRolls = RollCommanderDices(attackingTroops);
            }
        }
        
        int[] defenderRolls = null;
        if (attackAction.Target.CommandersList.Count > 0)
        {
            defenderRolls = RollCommanderDices(defendingTroops);
        }
        
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

    private int Roll() => UnityEngine.Random.Range(1, 7);
    
    private int CommanderRoll() => UnityEngine.Random.Range(1, 9);

    private int[] RollDices(int dices)
    {
        // var rolls = new List<int>(dices);
        var rolls = new int[dices];

        for (int i = 0; i < dices; i++) 
            rolls[i] = Roll();

        return rolls;
    }
    
    private int[] RollCommanderDices(int dices)
    {
        // var rolls = new List<int>(dices);
        var rolls = new int[dices];

        for (int i = 0; i < dices; i++) 
            rolls[i] = CommanderRoll();

        return rolls;
    }
}