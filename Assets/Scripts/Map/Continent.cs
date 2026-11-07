using System.Collections.Generic;
using System.Linq;
using player;
using UnityEngine;

namespace Map
{
    public class Continent : MonoBehaviour
    {
        public string Name;
        public List<Territory> Territories = new();
        public int BonusTroops;
        public Color Color;


        private void Start()
        {
            SetupCheck();
        }

        private void SetupCheck()
        {
            if (Territories.Count == 0 && TerritoryRepository.Instance.Devastated.Count == 0)
                Debug.LogError($"Continent {Name} has no territories", this);

            var territory = Territories.FirstOrDefault(t => t.Continent != this);
            if (territory != null)
                Debug.LogError($"Continent {Name} has territory {territory.Name} that does not belong to it", this);
        
            if(BonusTroops == 0)
                Debug.LogWarning($"Continent {Name} has no bonus troops", this);
        }
    
    
        // A continent loses its devastated territories; the bonus needs all of the rest. One that was
        // devastated completely gives no bonus.
        public bool IsComplete()
        {
            if (Territories.Count == 0) return false;
            var owner = Territories.First().Owner;
            return owner != null && Territories.All(t => t.Owner == owner);
        }
    
        public Player GetOwner()
        {
            if (!IsComplete())
                return null;
            return Territories.First().Owner;
        }

        public bool IsOwnedByPlayer(Player player)
        {
            return player != null && Territories.Count > 0 && Territories.All(t => t.Owner == player);
        }


        //
        // [MenuItem("CONTEXT/Continent/load child territories")]
        // static void DoubleMass(MenuCommand command)
        // {
        //     Continent continent = (Continent)command.context;
        //     continent.Territories = new ();
        //     foreach (Transform child in continent.transform)
        //     {
        //         continent.Territories.Add(child.GetComponent<Territory>());
        //     }
        //     // not needed as Continent is not a prefab
        //     // PrefabUtility.RecordPrefabInstancePropertyModifications(continent);
        //
        // }

    }
}