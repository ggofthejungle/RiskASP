using System;
using System.Linq;
using System.Text;
using Actions;
using Cards;
using InvadeEarth.Rules;
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
            player.SetEnergy(player.GetEnergy() + IncomeRules.EnergyIncome(player.GetTerritoryCount(),
                Map.ContinentRepository.Instance.GetContinentBonusForPlayer(player)));
            
            OnTroopsToPlaceChanged?.Invoke();

            foreach (var territory in player.Territories)
            {
                if (territory.containsSpaceStation)
                    territory.AddTroops(IncomeRules.StationTroopGrant);
            }

            if (_remainingTroopsToPlace == 0) _gm.NextTurnPhase();


        }

        public void OnAction(Player player, PlayerAction action)
        {
            if (action is ReinforceAction placeTroopsAction)
            {
                if (!TroopMovement.TryPlace(placeTroopsAction.Territory.Troops, placeTroopsAction.Troops,
                        _remainingTroopsToPlace, out var troops, out var left))
                    return;
                var added = troops - placeTroopsAction.Territory.Troops;
                placeTroopsAction.Territory.AddTroops(added);
                _remainingTroopsToPlace = left;

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
            _gm.CommandCards?.RevealBought(player);
        }
    }
}