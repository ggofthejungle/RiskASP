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

        public void UseAbility(params object[] parameters)
        {
            _method.DynamicInvoke();
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