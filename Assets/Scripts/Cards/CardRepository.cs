using System;
using System.Collections.Generic;
using System.Linq;
using Expansions;
using Extensions;
using JetBrains.Annotations;
using Map;
using player;
using UnityEngine;
using Expansions;

namespace Cards
{
    public class CardRepository : MonoBehaviour
    {
        public static CardRepository Instance { get; private set; }

        private TerritoryRepository _tr;

        public Card[] AllCards { get; private set; }

        private readonly Dictionary<string, Card> _cardNameToCardMap = new();

        public HashSet<Card> CardsInDeck { get; private set; }

        public CardExchangeType[] ExchangeTypes;

        public Commander DiplomatCommander = new Commander(CommanderType.Diplomat);
        public Commander LandCommander = new Commander(CommanderType.LandCommander);
        public Commander NavalCommander = new Commander(CommanderType.NavalCommander);
        public Commander SpaceCommander = new Commander(CommanderType.SpaceCommander);
        public Commander NuclearCommander = new Commander(CommanderType.NuclearCommander);
        private Card card;
        Abilities abilities = new Abilities();
        Abilities.AbilityDelegate[] abiltyArr = new Abilities.AbilityDelegate[41];
        
        
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("More than one TerritoryRepository in scene");
                Destroy(gameObject);
            }
            else
                Instance = this;

            _tr = TerritoryRepository.Instance;

            AllCards = GenerateCards();
            CardsInDeck = new HashSet<Card>(AllCards);
            foreach (var card in AllCards)
            {
                CardsInDeck.Add(card);
                _cardNameToCardMap.Add(card.Name, card);
            }

            // ExchangeTypes = GenerateExchangeTypes().ToArray();
        }

        private void initalizeAbilities()
        {
            //Diplomat
            abiltyArr[0] = abilities.CeaseFire;
            abiltyArr[1] = abilities.ColonyInfluence;
            abiltyArr[2] = abilities.DecoyRevealed;
            abiltyArr[3] = abilities.EnergyCrisis;
            abiltyArr[4] = abilities.Evacuation;
            abiltyArr[5] = abilities.Redeployment;
            abiltyArr[6] = abilities.TerritorialStation;
            //Land
            abiltyArr[7] = abilities.AssembleMods;
            abiltyArr[8] = abilities.ColonyInfluence;
            abiltyArr[9] = abilities.FrequencyJam;
            abiltyArr[10] = abilities.LandDeathTrap;
            abiltyArr[11] = abilities.Reinforcements;
            abiltyArr[12] = abilities.ScoutForces;
            abiltyArr[13] = abilities.StealthMods;
            abiltyArr[14] = abilities.StealthStation;
            //Naval
            abiltyArr[15] = abilities.AssembleMods;
            abiltyArr[16] = abilities.ColonyInfluence;
            abiltyArr[17] = abilities.FrequencyJam;
            abiltyArr[18] = abilities.HiddenEnergy;
            abiltyArr[19] = abilities.Reinforcements;
            abiltyArr[20] = abilities.StealthMods;
            abiltyArr[21] = abilities.WaterDeathTrap;
            //Nuclear
            abiltyArr[22] = abilities.AquaBrother;
            abiltyArr[23] = abilities.Armageddon;
            abiltyArr[24] = abilities.AssassinBomb;
            abiltyArr[25] = abilities.NickyBoy;
            abiltyArr[26] = abilities.RocketStrikeLand;
            abiltyArr[27] = abilities.RocketStrikeMoon;
            abiltyArr[28] = abilities.RocketStrikeWater;
            abiltyArr[29] = abilities.ScatterBombLand;
            abiltyArr[30] = abilities.ScatterBombWater;
            abiltyArr[31] = abilities.ScatterBombMoon;
            abiltyArr[32] = abilities.TheMother;
            //Space
            abiltyArr[33] = abilities.AssembleMods;
            abiltyArr[34] = abilities.ColonyInfluence;
            abiltyArr[35] = abilities.EnergyExtraction;
            abiltyArr[36] = abilities.FrequencyJam;
            abiltyArr[37] = abilities.InvadeSurface;
            abiltyArr[38] = abilities.OrbitalMines;
            abiltyArr[39] = abilities.Reinforcements;
            abiltyArr[40] = abilities.StealthMods;

        }


        private Card[] GenerateCards()
        {
           //6 commanders each with their own card deck 
           initalizeAbilities();
           
           Card[] cards = new Card[41];
           int count = 0;
           foreach (CardType cardType in Enum.GetValues(typeof(CardType)))
           {
               // CardType and ability array need to line up
               card = new Card(abiltyArr[count], cardType);
               cards[count] = card;
               count++;

           }
           AssignCardsToCommanders(cards);
           return cards;
           
           

            // var territories = _tr.Territories.ToArray();
            // territories.Shuffle();
            
            // for (int i = 0; i < 14; i++)
            // {
            // //   
            // //     cards[i + 14] = new Card(CardType.Cavalry, territories[i + 14]);
            // //     cards[i + 28] = new Card(CardType.Artillery, territories[i + 28]);
            // // }

            // cards[42] = new Card(CardType.Wild, name: "Wild_1");
            // cards[43] = new Card(CardType.Wild, name: "Wild_2");
            // cards.Shuffle();
        }

        private void AssignCardsToCommanders(Card[] cards)
        {
            for (int x = 0; x < cards.Length; x++) 
            {
                if(x < 7)
                    DiplomatCommander.AddCard(cards[x]);
                else if(x < 16)
                    LandCommander.AddCard(cards[x]);
                else if(x < 24)
                    NavalCommander.AddCard(cards[x]);
                else if(x < 34)
                    NuclearCommander.AddCard(cards[x]);
                else
                    SpaceCommander.AddCard(cards[x]);
            }
            
        }

        // private IEnumerable<CardExchangeType> GenerateExchangeTypes()
        // {
        //     return new CardExchangeType[]
        //     {
        //         new(requiredCards: new() { { CardType.Artillery, 3 } }, troops: 4),
        //         new(requiredCards: new() { { CardType.Infantry,  3 } }, troops: 6),
        //         new(requiredCards: new() { { CardType.Cavalry,   3 } }, troops: 8),
        //         new(requiredCards: new()
        //             { { CardType.Artillery, 1 }, { CardType.Infantry, 1 }, { CardType.Cavalry, 1 } }, troops: 10),
        //         new(requiredCards: new() { { CardType.Wild, 1 }, { CardType.Artillery, 2 } }, troops: 12),
        //         new(requiredCards: new() { { CardType.Wild, 1 }, { CardType.Infantry,  2 } }, troops: 12),
        //         new(requiredCards: new() { { CardType.Wild, 1 }, { CardType.Cavalry,   2 } }, troops: 12)
        //     };
        // }

        public Card DrawRandomCard()
        {
            if (CardsInDeck.Count == 0)
                return null;

            var card = CardsInDeck.ElementAt(UnityEngine.Random.Range(0, CardsInDeck.Count));
            CardsInDeck.Remove(card);
            return card;
        }

        public void ReturnCardsToDeck(IEnumerable<Card> cards)
        {
            foreach (var card in cards)
                CardsInDeck.Add(card);
        }


        // public List<CardExchange> GetBestExchanges(player.Player player)
        // {
        //     // var bestExchanges = new List<CardExchange>();
        //     // var cards = player.Cards;
        //     //
        //     // foreach (var cardExchangeType in ExchangeTypes)
        //     // {
        //     //     var cardExchange = cardExchangeType.GetBestExchange(cards, player);
        //     //     if (cardExchange != null)
        //     //         bestExchanges.Add(cardExchange);
        //     // }
        //     //
        //      return new List<CardExchange>();
        // }

        [CanBeNull]
        public CardExchangeType GetExchangeType(Card[] cards)
        {
            if (cards.Length != 3)
                return null;

            return ExchangeTypes.FirstOrDefault(exchangeType => exchangeType.CanExchange(cards));
        }
        
        
        public bool CanExchange(Card[] cards)
        {
            if (cards.Length != 3)
                return false;

            return ExchangeTypes.Any(exchangeType => exchangeType.CanExchange(cards));
        }

        public int GetExchangeTypeIndex(Card[] cards)
        {
            if (cards.Length != 3)
                return -1;

            for (var index = 0; index < ExchangeTypes.Length; index++)
            {
                var exchangeType = ExchangeTypes[index];
                if (exchangeType.CanExchange(cards)) return index;
            }

            return -1;
        }

        public int GetExchangeTypeIndex(CardExchangeType exchangeType)
        {
            for (var index = 0; index < ExchangeTypes.Length; index++)
            {
                if (ExchangeTypes[index] == exchangeType)
                    return index;
            }

            return -1;
        }


        public Card GetCardByName(string name)
        {
            return _cardNameToCardMap[name];
        }

        
        
        [CanBeNull]
        public CardExchange GetExchangeFor(Card[] selectedCards, Player currentPlayer)
        {
            var exchangeType = GetExchangeType(selectedCards);
            if (exchangeType == null)
                return null;

            return new CardExchange(selectedCards, exchangeType, currentPlayer);
        }
    }
}