using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime.Misc;
using Cards;
using Expansions;
using Extensions;
using InvadeEarth.Rules;
using Map;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.Serialization;

namespace player
{
    public class Player : MonoBehaviour
    {
        [SerializeField] private string _name;
        public string Name => _name;

        private readonly HashSet<Territory> _territories = new();
        public IEnumerable<Territory> Territories => _territories;


        public IReadOnlyList<Card> Cards => _cards;
        [SerializeField] private List<Card> _cards = new();

        public Dictionary<CardType, int> CardTypeCountMap { get; } = new();

        public int _energy;
        public int _energyBid;

        public ArrayList<Commander> _CommandersArray = new();
        
        public int _spaceStationCount;

        public PlayerColor Color;
        public bool canPlayCommandCards = true;




        public Action<(Player eliminatedBy, Player eliminated)> OnEliminated;
        public Action OnCardsChanged;


        private void Awake()
        {
            // OnCardsChanged += () => BestCardExchangeCombinations = CardRepository.Instance.GetBestExchanges(this);
        }

        // The 2-player neutral army. It defends and never takes a turn.
        public bool Neutral { get; set; }

        public bool IsHuman() => GetComponent<HumanPlayer>() != null;
        public bool IsBot() => GetComponent<BotPlayer>() != null;

        public bool IsDead() => _territories.Count == 0;
        public bool IsAlive() => _territories.Count > 0;


        public int GetTroopsCountInTerritories()
        {
            return _territories.Sum(territory => territory.Troops);
        }

        public int GetTerritoryCount()
        {
            return _territories.Count;
        }

        public bool HasTerritory(Territory territory)
        {
            return _territories.Contains(territory);
        }

        public int GetTotalTroopBonus() =>
            IncomeRules.TroopBonus(GetTerritoryCount(), ContinentRepository.Instance.GetContinentBonusForPlayer(this));

        public void SetName(string newName)
        {
            _name = newName;
            gameObject.name = $"Player {newName}";
            if (IsBot() && !newName.Contains("Bot"))
            {
                _name += " (Bot)";
                gameObject.name += " (Bot)";
            }
        }

        public void SetEnergy(int energy)
        {
            _energy = energy;
        }

        public int GetEnergy()
        {
            return _energy;
        }

        public int DistributeNTroopsPerTerritory(int troopsPerTerritory, int troops)
        {
            var territories = _territories.ToList();
            territories.Shuffle();
            for (int i = 0; i < territories.Count && troops > 0; i++)
            {
                int troopsToAdd = Mathf.Min(troopsPerTerritory, troops);
                territories[i].AddTroops(troopsToAdd);
                troops -= troopsToAdd;
            }

            return troops;
        }

        public void RandomlyDistributeTroops(int troops)
        {
            var list = _territories.ToList();
            foreach (var pick in TroopMovement.ScatterPicks(list.Count, troops, GameRandom.Current))
                list[pick].AddTroops(1);
        }

        public void AddTerritory(Territory territory) => _territories.Add(territory);

        public void RemoveTerritory(Territory territory)
        {
            _territories.Remove(territory);
        }

        // A commander is "in play" when it's on the map in a territory this player controls (2210 rules).
        public bool HasCommanderInPlay(CommanderType type) =>
            _territories.Any(t => t.CommandersList != null && t.CommandersList.Any(c => c.CommanderType == type));

        public void PurchaseCommanders(Commander commander)
        {
            _energy -= 3;
            SetCommandersArray(commander);
        }

        public void PurchaseCommandersCards(Card card)
        {
            //comeback
            _energy -= 1;
            _cards.Add(card);
        }

        public void SetCommandersArray(Commander commander)
        {
            _CommandersArray.Add(commander);
        }

        public ArrayList<Commander> GetCommandersArray()
        {
            return _CommandersArray;
        }


        public void ClearTroops()
        {
            foreach (var territory in _territories) territory.SetTroops(0);
        }

        #region Cards

        private void AddCardInternal(Card card)
        {
            if (Cards.Contains(card))
                throw new Exception($"Player {Name} already has card {card}");
            _cards.Add(card);

            // CardTypeCountMap.TryAdd(LandCardType.Type, 0);
            // CardTypeCountMap[LandCardType.Type]++;
        }

        public void AddCard(Card card)
        {
            AddCardInternal(card);
            OnCardsChanged?.Invoke();
        }


        public void AddCards(IEnumerable<Card> cards)
        {
            foreach (var card in cards) AddCardInternal(card);
            OnCardsChanged?.Invoke();
        }

        private void RemoveCardInternal(Card card)
        {
            if (!Cards.Contains(card))
                throw new Exception($"Player {Name} doesn't have card {card}");
            _cards.Remove(card);
            // CardTypeCountMap[card.Type]--;
        }

        public void RemoveCard(Card card)
        {
            RemoveCardInternal(card);
            OnCardsChanged?.Invoke();
        }

        public void RemoveCards(IEnumerable<Card> cards)
        {
            foreach (var card in cards) RemoveCardInternal(card);
            OnCardsChanged?.Invoke();
        }


        #endregion
    }
}
