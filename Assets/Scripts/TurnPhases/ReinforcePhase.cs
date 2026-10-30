using System;
using System.Linq;
using System.Text;
using Actions;
using Cards;
using Map;
using player;
using UnityEngine;

namespace TurnPhases
{
    public class ReinforcePhase : IPhase
    {
        public string Name => "Reinforce";

        private readonly GameManager _gm;
        private readonly CardRepository _cardRepository;

        public int RemainingTroopsToPlace => _remainingTroopsToPlace;
        private int _remainingTroopsToPlace;



        public Action OnTroopsToPlaceChanged;
        public Action<ReinforceAction> OnTroopsPlaced;


        public ReinforcePhase(GameManager gm, CardRepository cardRepository)
        {
            _gm = gm;
            _cardRepository = cardRepository;

        }

        public void Start(Player player)
        {
            _remainingTroopsToPlace = player.GetTotalTroopBonus();
            player.SetEnergy( player.GetEnergy() + CalculateEnergyOnTerritories(player));
            
            OnTroopsToPlaceChanged?.Invoke();

            foreach (var territory in player.Territories)
            {
                if (territory.containsSpaceStation)
                {
                    territory.AddTroops(1);
                }
            }

            if (_remainingTroopsToPlace == 0) _gm.NextTurnPhase();


        }

        public void OnAction(Player player, PlayerAction action)
        {
            if (action is ReinforceAction placeTroopsAction)
            {
                placeTroopsAction.Territory.AddTroops(placeTroopsAction.Troops);
                _remainingTroopsToPlace -= placeTroopsAction.Troops;

                OnTroopsPlaced?.Invoke(placeTroopsAction);
                OnTroopsToPlaceChanged?.Invoke();
            }
            else if (action is EndPhaseAction)
            {
                if (_remainingTroopsToPlace > 0)
                {
                    Debug.Log(
                        $"Player {player.Name} ended Reinforce phase with {_remainingTroopsToPlace} troops to place, distributing randomly");
                    player.RandomlyDistributeTroops(_remainingTroopsToPlace);
                    _remainingTroopsToPlace = 0;

                    OnTroopsToPlaceChanged?.Invoke();
                }

                _gm.NextTurnPhase();
                return;
            }
            else
                Debug.LogError($"ReinforcePhase:  Player {player.Name} Received action of type {action.GetType().Name}");

            if (_remainingTroopsToPlace == 0)
                _gm.NextTurnPhase();
        }

       
        public void End(Player player)
        {
        }
        

        private int CalculateEnergyOnTerritories(Player player)
        {
            int numOfTerritories = player.GetTerritoryCount();

            if (numOfTerritories < 12)
                return 3;
            
            return numOfTerritories / 3;
        }
    }
}