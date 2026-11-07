using System;
using Expansions;
using JetBrains.Annotations;
using Map;
using player;

namespace Cards
{
    [Serializable]
    public class Card
    {
        public CardType CardType;
        private CommanderType CommanderType;
        public string Name;
        public int Id; // unique per physical card (decks have copies)
        private Delegate _method;

        public Card(Delegate method, CardType cardType)
        {
            _method = method;
            Enum  cardName = cardType;
            CardType = cardType;
            Name = cardName.ToString();
        }
        
        public void AssignCommander(CommanderType commanderType)
        {
            CommanderType = commanderType;
        }

        // Online: another player's card this device isn't allowed to see. Only its deck is known.
        public bool IsHidden { get; private set; }
        static int _nextHiddenId = -1;

        public static Card FaceDown(CommanderType deck)
        {
            var card = new Card(null, default) { Name = "Face-down card", Id = _nextHiddenId--, IsHidden = true };
            card.AssignCommander(deck);
            return card;
        }

        public CommanderType Commander => CommanderType;
        public CardSpec Spec => CardSpec.For(CardType);
        public int PowerUpCost => DeckLists.CostOf(CardType);
        public CardTiming Timing => IsHidden ? CardTiming.NotImplemented : Spec.Timing;
        public string AbilityName => _method?.Method.Name;
        public int AbilityParameterCount => _method?.Method.GetParameters().Length ?? 0;

        public void UseAbility(params object[] parameters)
        {
            // Pass the targets through (was DynamicInvoke() with no arguments, which threw for any card with a target).
            _method?.DynamicInvoke(parameters);
        }

        // if (name != null)
            //     Name = name;
            // else
            //     Name = territory != null ? territory.Name : type.ToString();
            // if(type == CardType.Wild && territory != null)
            //     throw new ArgumentException("Wild card cannot have a territory");
            //
            // if (type != CardType.Wild && territory == null)
            //     throw new ArgumentException("Non-wild card must have a territory");
        
    }
}