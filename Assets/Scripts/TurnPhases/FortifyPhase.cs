using System;
using Actions;
using Map;
using player;
using UnityEngine;

namespace TurnPhases
{
    public class FortifyPhase : IPhase
    {
        public string Name => "Fortify";

        private readonly GameManager _gm;

        public Action<FortifyAction> OnFortifyAction;
        
        public FortifyPhase(GameManager gm)
        {
            _gm = gm;
        }

        // Extra free moves granted this turn (Redeployment card).
        public int ExtraMoves { get; private set; }
        public void AddExtraMove() => ExtraMoves++;

        public void Start(Player player)
        {
            ExtraMoves = 0;
        }

        public void OnAction(Player player, PlayerAction action)
        {
            if (action is FortifyAction fortifyAction)
            {
                fortifyAction.From.RemoveTroops(fortifyAction.MovedTroops);
                fortifyAction.To.AddTroops(fortifyAction.MovedTroops);
                FortifyAction.MoveCommanders(fortifyAction.From, fortifyAction.To, fortifyAction.MovingCommanders);
                OnFortifyAction?.Invoke(fortifyAction);
                if (ExtraMoves > 0)
                {
                    ExtraMoves--; // Redeployment: one more free move this turn
                    return;
                }
                _gm.NextTurnPhase();
            }
            else if (action is EndPhaseAction)
            {
                _gm.NextTurnPhase();
            }
            else
                Debug.LogWarning($"FortifyPhase: Received action of type {action.GetType().Name}");
        }


        public void End(Player player)
        {
        }
    }
}