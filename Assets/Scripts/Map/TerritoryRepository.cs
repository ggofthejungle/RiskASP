using System;
using System.Collections.Generic;
using System.Linq;
using Extensions;
using player;
using UnityEngine;

namespace Map
{
    public class TerritoryRepository : MonoBehaviour
    {
        public static TerritoryRepository Instance { get; private set; }

        public readonly Dictionary<string, Territory> TerritoriesMap = new();
        public readonly List<Territory> Territories = new();
        public readonly Dictionary<Territory, int> TerritoryToIslandMap = new();


        [SerializeField] private bool _loadFromChildren = true;


        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("More than one TerritoryRepository in scene");
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }

            if (_loadFromChildren)
                LoadTerritoriesFromChildren();

            SubscribeToCallbacks();
        }

        private void SubscribeToCallbacks()
        {
            foreach (var territory in Territories)
            {
                territory.OnOwnerChanged += (oldOwner, newOwner) =>
                {
                    OnTerritoryOwnerChanged(territory, oldOwner, newOwner);
                };
            }
        }

        private void OnTerritoryOwnerChanged(Territory territory, Player oldOwner, Player newOwner)
        {
            CalculateTerritoryReachability();
        }

        private void CalculateTerritoryReachability()
        {
            // find territories islands (connected territories)
            // using BFS

            TerritoryToIslandMap.Clear();
            var islandIndex = 0;
            foreach (var territory in Territories)
            {
                if (TerritoryToIslandMap.ContainsKey(territory))
                    continue;

                TerritoryReachabilityBFS(territory, islandIndex);
                islandIndex++;
            }
        }

        private void TerritoryReachabilityBFS(Territory territory, int islandIndex)
        {
            var queue = new Queue<Territory>();
            queue.Enqueue(territory);
            while (queue.Any())
            {
                var currentTerritory = queue.Dequeue();
                if (TerritoryToIslandMap.ContainsKey(currentTerritory))
                    continue;

                TerritoryToIslandMap.Add(currentTerritory, islandIndex);
                // currentTerritory.GetComponent<TerritoryGraphics>()._territoryNameText.text = islandIndex.ToString();
                foreach (var neighbor in currentTerritory.NeighbourTerritories)
                {
                    if (neighbor.Owner == currentTerritory.Owner)
                        queue.Enqueue(neighbor);
                }
            }
        }

        private void LoadTerritoriesFromChildren()
        {
            if (Territories.Any())
                throw new Exception("Territories already loaded");

            foreach (var territory in GetComponentsInChildren<Territory>())
            {
                Add(territory);
            }
        }

        public void Add(Territory territory)
        {
            Territories.Add(territory);
            TerritoriesMap.Add(territory.Name, territory);
        }

        public void RandomlyAssignTerritories(List<Player> players)
        {
            var territories = Territories.ToList();
            territories.Shuffle();

            var playerOrder = Enumerable.Range(0, players.Count).ToList();
            playerOrder.Shuffle();

            var playerIndex = 0;
            foreach (var territory in territories)
            {
                var player = players[playerOrder[playerIndex]];
                territory.SetOwner(player);
                player.AddTerritory(territory);

                playerIndex = (playerIndex + 1) % players.Count;
            }
        }

        // Devastated territories: still drawn on the map, but gone from the game. No owner, no units, no
        // borders (its neighbours lose the connection, as if it was never there), no continent.
        public readonly List<Territory> Devastated = new();

        public void Devastate(Territory territory)
        {
            if (territory == null || territory.Devastated) return;
            if (territory.Owner != null) territory.Owner.RemoveTerritory(territory);
            foreach (var neighbour in territory.NeighbourTerritories)
                neighbour.NeighbourTerritories.Remove(territory);
            if (territory.Continent != null) territory.Continent.Territories.Remove(territory);
            Territories.Remove(territory);
            Devastated.Add(territory);
            territory.MarkDevastated();
            Frontiers.DropLinks(territory);
            var collider = territory.GetComponent<Collider2D>();
            if (collider != null) collider.enabled = false;
            CalculateTerritoryReachability();
        }

        public Territory FromName(string territoryName)
        {
            return TerritoriesMap[territoryName];
        }

        // One fortify may cross any path of your own territories. An enemy or empty territory breaks the path.
        // A space station linked to a landing site is already a neighbour, so Moon and Earth connect that way.
        public bool CanFortifyTerritory(Territory from, Territory to)
        {
            if (from == null || to == null || from == to || from.Owner == null || from.Owner != to.Owner) return false;
            var seen = new System.Collections.Generic.HashSet<Territory> { from };
            var queue = new System.Collections.Generic.Queue<Territory>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var here = queue.Dequeue();
                foreach (var next in here.NeighbourTerritories)
                {
                    if (next == to) return true;
                    if (next.Owner != from.Owner || !seen.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
            return false;
        }
    }
}