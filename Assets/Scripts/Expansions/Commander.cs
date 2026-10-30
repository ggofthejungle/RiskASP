using System;
using System.Collections.Generic;
using Cards;
using JetBrains.Annotations;
using Map;
using player;

namespace Expansions
{
    [Serializable]
    public class Commander
    {
        
        public CommanderType CommanderType;
        public List<Card> Cards;

        public Commander(CommanderType commanderType)
        {
            CommanderType = commanderType;
            Cards = new List<Card>();
            
            
            // if (Type == CommanderType.Diplomat)
            // {
            //     _DiplomatCommanderCardAbilities = new DiplomatCommanderCardAbilities();
            // }
            // else if (Type == CommanderType.LandCommander)
            // {
            //     _LandCommanderCardAbilities = new LandCommanderCardAbilities();
            // }
            // else if (Type == CommanderType.NavalCommander)
            // {
            //     _NavalCommandersCardAbilities = new NavalCommandersCardAbilities();
            // }
            // else if (Type == CommanderType.NuclearCommander)
            // {
            //     _NuclearCommanderCardsAbilities = new NuclearCommanderCardsAbilities();
            // }
            // else
            // {
            //     _SpaceCommanderCardsAbilities = new SpaceCommanderCardsAbilities();
            // }

        }
        
        public void AddCard(Card card)
        {
            Cards.Add(card);
            card.AssignCommander(CommanderType);
        }
    }
}