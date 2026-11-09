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
            CardType = cardType;
            Name = DisplayName(cardType);
        }

        // "RocketStrikeLand" -> "Rocket Strike", "StealthModsL" -> "Stealth MODs". The deck variant suffix is
        // dropped; "Land Death Trap" keeps its word because it's part of the card's name.
        public static string DisplayName(CardType type)
        {
            var id = type.ToString();
            foreach (var suffix in new[] { "Land", "Moon", "Water", "Na", "L", "D", "S" })
                if (id.Length > suffix.Length && id.EndsWith(suffix) && char.IsLower(id[id.Length - suffix.Length - 1]))
                {
                    id = id.Substring(0, id.Length - suffix.Length);
                    break;
                }
            var words = new System.Text.StringBuilder();
            for (var i = 0; i < id.Length; i++)
            {
                if (i > 0 && char.IsUpper(id[i])) words.Append(' ');
                words.Append(id[i]);
            }
            return words.ToString().Replace("Mods", "MODs").Replace("Mod ", "MOD ");
        }
        
        public void AssignCommander(CommanderType commanderType)
        {
            CommanderType = commanderType;
        }

        // Online: another player's card this device isn't allowed to see. Only its deck is known.
        public bool IsHidden { get; private set; }
        // Bought this turn and not looked at yet. The owner has the card; the name stays hidden until they look.
        public bool Sealed { get; private set; }
        public void Seal() => Sealed = true;
        public void Unseal() => Sealed = false;
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