using System;
using Expansions;
using JetBrains.Annotations;
using Map;

namespace Cards
{
    [Serializable]
    public class Card
    {
        public CardType CardType;
        private CommanderType CommanderType;
        private Abilities.AbilityDelegate Ability;
        public string Name;

        public Card(Abilities.AbilityDelegate ability, CardType cardType)
        {
            Enum  cardName = cardType;
            Ability = ability;
            CardType = cardType;
            Name = cardName.ToString();
        }
        
        public void AssignCommander(CommanderType commanderType)
        {
            CommanderType = commanderType;
        }

        public void UseAbility()
        {
            Ability.Invoke();
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