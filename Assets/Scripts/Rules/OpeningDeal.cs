using System;
using System.Linq;

namespace InvadeEarth.Rules
{
    // The opening map: who owns each territory, and how many units sit there.
    // Random calls match the game's shuffle, then one-each, then the leftovers one at a time.
    public static class OpeningDeal
    {
        public static int TroopsForPlayerCount(int players) => players switch
        {
            2 => 40,
            3 => 35,
            4 => 30,
            5 => 25,
            6 => 20,
            _ => 0,
        };

        public struct Result
        {
            public int[] OwnerOf;
            public int[] Troops;
        }

        // Territory indexes are the caller's list order. Owner ids are indexes into the player list.
        public static Result Deal(int territoryCount, int playerCount, IRandom random)
        {
            var owner = new int[territoryCount];
            var troops = new int[territoryCount];
            if (territoryCount == 0 || playerCount == 0) return new Result { OwnerOf = owner, Troops = troops };

            var order = Enumerable.Range(0, territoryCount).ToArray();
            Shuffle(order, random);
            var playerOrder = Enumerable.Range(0, playerCount).ToArray();
            Shuffle(playerOrder, random);

            var playerIndex = 0;
            foreach (var territory in order)
            {
                owner[territory] = playerOrder[playerIndex];
                playerIndex = (playerIndex + 1) % playerCount;
            }

            var budget = TroopsForPlayerCount(playerCount);
            for (var p = 0; p < playerCount; p++)
            {
                var insertion = order.Where(i => owner[i] == p).ToArray();
                if (insertion.Length == 0) continue;
                var firstPass = (int[])insertion.Clone();
                Shuffle(firstPass, random);
                var left = budget;
                for (var i = 0; i < firstPass.Length && left > 0; i++)
                {
                    troops[firstPass[i]] += 1;
                    left--;
                }
                foreach (var pick in TroopMovement.ScatterPicks(insertion.Length, left, random))
                    troops[insertion[pick]] += 1;
            }

            return new Result { OwnerOf = owner, Troops = troops };
        }

        // Same Fisher-Yates the game uses on lists.
        public static void Shuffle(int[] list, IRandom random)
        {
            var n = list.Length;
            while (n > 1)
            {
                n--;
                var k = random.Range(0, n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
        }
    }
}
