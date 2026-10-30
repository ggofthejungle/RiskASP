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
using Unity.VisualScripting;

namespace Cards
{
    public class CardRepository : MonoBehaviour
    {
        public static CardRepository Instance { get; private set; }

        private TerritoryRepository _tr;

        public Card[] AllCards { get; private set; }


        public HashSet<Card> CardsInDeck { get; private set; }


        public Commander DiplomatCommander = new Commander(CommanderType.Diplomat);
        public Commander LandCommander = new Commander(CommanderType.LandCommander);
        public Commander NavalCommander = new Commander(CommanderType.NavalCommander);
        public Commander SpaceCommander = new Commander(CommanderType.SpaceCommander);
        public Commander NuclearCommander = new Commander(CommanderType.NuclearCommander);
        private Card card;
        Abilities abilities = new Abilities();
        
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


            // ExchangeTypes = GenerateExchangeTypes().ToArray();
        }

        private Card[] GenerateCards()
        {
            // 2210: one deck per commander, with copies (DeckLists). Each physical card is its own Card.
            var cards = new List<Card>();
            foreach (var deck in DeckLists.DecksInPlay)
                foreach (var (type, copies) in DeckLists.For(deck))
                    for (int i = 0; i < copies; i++)
                        cards.Add(new Card(abilities.FindFor(type), type) { Id = cards.Count });
            AssignCardsToCommanders(cards.ToArray());
            return cards.ToArray();
        }

        private void AssignCardsToCommanders(Card[] cards)
        {
            // By the card's group in the CardType enum (the old index ranges 7/16/24/34 were off by one or two).
            foreach (var c in cards)
            {
                switch (CommanderFor(c.CardType))
                {
                    case CommanderType.Diplomat: DiplomatCommander.AddCard(c); break;
                    case CommanderType.LandCommander: LandCommander.AddCard(c); break;
                    case CommanderType.NavalCommander: NavalCommander.AddCard(c); break;
                    case CommanderType.NuclearCommander: NuclearCommander.AddCard(c); break;
                    default: SpaceCommander.AddCard(c); break;
                }
            }
        }

        // Which commander's deck a card belongs to, from the groups in CardType.cs.
        public static CommanderType CommanderFor(CardType cardType)
        {
            if (cardType <= CardType.TerritorialStation) return CommanderType.Diplomat;
            if (cardType <= CardType.StealthStation) return CommanderType.LandCommander;
            if (cardType <= CardType.WaterDeathTrap) return CommanderType.NavalCommander;
            if (cardType <= CardType.TheMother) return CommanderType.NuclearCommander;
            return CommanderType.SpaceCommander;
            
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

    }
}