using System;
using Cards;
using JetBrains.Annotations;
using Map;

namespace Expansions
{
    [Serializable]
    public class Commander
    {
        public CommanderType Type;
        

        public Commander(CommanderType type)
        {
            Type = type;
           
        }
    }
}