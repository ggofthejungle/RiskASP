using System;
using System.Collections.Generic;
using System.Linq;

namespace InvadeEarth.Rules
{
    public struct Placement
    {
        public int Territory;
        public int Amount;
    }

    public struct BotAttack
    {
        public int From;
        public int To;
        public int Mods;
    }

    public struct BotMove
    {
        public int From;
        public int To;
        public int Amount;
    }

    // MM-2: what a bot does with its MODs (place, attack, move in, fortify), without the ASP solver, so bots
    // run on mobile and WebGL too. Reads only the public board, and the same board always gives the same
    // answer (ties go to the lower index), so every device in a lockstep match agrees.
    public static class BotTactics
    {
        // Attack while the units that can leave outnumber the defenders by at least this much.
        public const int Margin = 1;
        // Share of new MODs that go to the main attack front; the rest shore up threatened borders.
        public const double FrontShare = 2.0 / 3.0;

        // Water needs the Naval Commander in play. The Moon needs the Space Commander. Either end counts.
        public static bool CommanderLocks(BoardTerritory from, BoardTerritory to, bool navalInPlay, bool spaceInPlay)
        {
            if (from == null || to == null) return false;
            if (!navalInPlay && (from.Terrain == "Sea" || to.Terrain == "Sea")) return true;
            if (!spaceInPlay && (from.Terrain == "Moon" || to.Terrain == "Moon")) return true;
            return false;
        }

        public static List<Placement> Place(BoardTerritory[] map, int me, int count, IReadOnlyList<int> continentBonus = null)
        {
            var result = new List<Placement>();
            if (count <= 0) return result;
            var mine = Owned(map, me).ToList();
            if (mine.Count == 0) return result;
            var borders = mine.Where(i => IsBorder(map, i, me)).ToList();
            if (borders.Count == 0)
            {
                result.Add(new Placement { Territory = mine[0], Amount = count });
                return result;
            }

            var added = new int[map.Length];
            var front = Best(borders, i => FrontScore(map, i, me, continentBonus));
            var toFront = Math.Min(count, (int)Math.Ceiling(count * FrontShare));
            added[front] += toFront;
            for (var left = count - toFront; left > 0; left--)
                added[Best(borders, i => Threat(map, i, me) - map[i].Troops - added[i])]++;

            for (var i = 0; i < map.Length; i++)
                if (added[i] > 0) result.Add(new Placement { Territory = i, Amount = added[i] });
            return result;
        }

        // The best attack right now, or null to stop. Owners in blockedOwners can't be attacked (Cease Fire).
        public static BotAttack? ChooseAttack(BoardTerritory[] map, int me, IReadOnlyList<int> continentBonus = null,
            ICollection<int> blockedOwners = null, bool navalInPlay = true, bool spaceInPlay = true,
            ICollection<int> moonToLand = null)
        {
            BotAttack? best = null;
            var bestScore = double.MinValue;
            for (var from = 0; from < map.Length; from++)
            {
                if (map[from].OwnerId != me) continue;
                var available = TroopMovement.Available(map[from].Troops);
                if (available < 1) continue;
                foreach (var to in map[from].Neighbours)
                {
                    var target = map[to];
                    var empty = target.OwnerId < 0 && target.Troops == 0;
                    if (target.OwnerId == me || (target.OwnerId < 0 && !empty)) continue;
                    if (!empty && blockedOwners != null && blockedOwners.Contains(target.OwnerId)) continue;
                    if (CommanderLocks(map[from], target, navalInPlay, spaceInPlay)) continue;
                    if (map[from].Terrain == "Moon" && target.Terrain == "Land"
                        && (moonToLand == null || !moonToLand.Contains(to))) continue;
                    if (!empty && available < target.Troops + Margin) continue;
                    var score = empty
                        ? 2 + ContinentPull(map, to, me, continentBonus)
                        : available - target.Troops + ContinentPull(map, to, me, continentBonus);
                    if (!empty && Owned(map, target.OwnerId).Count() == 1) score += 3; // knocks a player out
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = new BotAttack { From = from, To = to, Mods = empty ? 1 : Math.Min(BattleRules.MaxAttackDice, available) };
                    }
                }
            }
            return best;
        }

        // How many MODs follow a conquest from `from` into `to` (already owned by the bot): all of them if the
        // origin is now safe, the minimum if the new territory is, otherwise half.
        public static int MoveIn(BoardTerritory[] map, int from, int to, int min)
        {
            var me = map[from].OwnerId;
            var available = TroopMovement.Available(map[from].Troops);
            if (available <= min) return min;
            if (!IsBorder(map, from, me)) return available;
            if (!IsBorder(map, to, me)) return min;
            return Math.Max(min, (available + 1) / 2);
        }

        // One fortify move: the biggest stack behind the lines walks one step toward the nearest border.
        public static BotMove? Fortify(BoardTerritory[] map, int me)
        {
            var distance = DistanceToBorder(map, me);
            BotMove? best = null;
            var bestAmount = 0;
            for (var from = 0; from < map.Length; from++)
            {
                if (map[from].OwnerId != me || distance[from] <= 0) continue;
                var available = TroopMovement.Available(map[from].Troops);
                if (available <= bestAmount) continue;
                var step = -1;
                foreach (var n in map[from].Neighbours)
                {
                    if (map[n].OwnerId != me || distance[n] < 0 || distance[n] >= distance[from]) continue;
                    if (step < 0 || distance[n] < distance[step]
                        || (distance[n] == distance[step] && Threat(map, n, me) > Threat(map, step, me)))
                        step = n;
                }
                if (step < 0) continue;
                var dest = step;
                for (var guard = 0; distance[dest] > 0 && guard < map.Length; guard++)
                {
                    var next = -1;
                    foreach (var n in map[dest].Neighbours)
                    {
                        if (map[n].OwnerId != me || distance[n] < 0 || distance[n] >= distance[dest]) continue;
                        if (next < 0 || distance[n] < distance[next]) next = n;
                    }
                    if (next < 0) break;
                    dest = next;
                }
                bestAmount = available;
                best = new BotMove { From = from, To = dest, Amount = available };
            }
            return best;
        }

        // Steps from each own territory to the nearest own border territory (0 = a border, -1 = not owned
        // or cut off from every border).
        public static int[] DistanceToBorder(BoardTerritory[] map, int me)
        {
            var distance = Enumerable.Repeat(-1, map.Length).ToArray();
            var queue = new Queue<int>();
            for (var i = 0; i < map.Length; i++)
                if (map[i].OwnerId == me && IsBorder(map, i, me))
                {
                    distance[i] = 0;
                    queue.Enqueue(i);
                }
            while (queue.Count > 0)
            {
                var i = queue.Dequeue();
                foreach (var n in map[i].Neighbours)
                {
                    if (map[n].OwnerId != me || distance[n] >= 0) continue;
                    distance[n] = distance[i] + 1;
                    queue.Enqueue(n);
                }
            }
            return distance;
        }

        static IEnumerable<int> Owned(BoardTerritory[] map, int player) =>
            Enumerable.Range(0, map.Length).Where(i => map[i].OwnerId == player);

        static bool IsBorder(BoardTerritory[] map, int i, int me) =>
            map[i].Neighbours.Any(n => map[n].OwnerId != me);

        // The biggest enemy stack next to a territory.
        static int Threat(BoardTerritory[] map, int i, int me) =>
            map[i].Neighbours.Where(n => map[n].OwnerId >= 0 && map[n].OwnerId != me)
                .Select(n => map[n].Troops).DefaultIfEmpty(0).Max();

        // How good a border territory is as a place to attack from: a weak neighbour, ideally in a continent
        // the bot is close to holding.
        static double FrontScore(BoardTerritory[] map, int i, int me, IReadOnlyList<int> continentBonus)
        {
            var best = double.MinValue;
            foreach (var n in map[i].Neighbours)
            {
                if (map[n].OwnerId == me) continue;
                var empty = map[n].OwnerId < 0;
                var worth = empty ? 1.5 : map[i].Troops * 0.5 - map[n].Troops;
                best = Math.Max(best, worth + ContinentPull(map, n, me, continentBonus));
            }
            return best;
        }

        // Extra value of taking a territory: its continent's bonus, scaled by how much of it the bot already holds.
        static double ContinentPull(BoardTerritory[] map, int i, int me, IReadOnlyList<int> continentBonus)
        {
            var c = map[i].Continent;
            if (c < 0 || continentBonus == null || c >= continentBonus.Count) return 0;
            int total = 0, owned = 0;
            foreach (var t in map)
            {
                if (t.Continent != c) continue;
                total++;
                if (t.OwnerId == me) owned++;
            }
            if (total == 0) return 0;
            var pull = continentBonus[c] * (double)owned / total;
            if (owned == total - 1) pull += continentBonus[c]; // this conquest completes it
            return pull;
        }

        static int Best(List<int> candidates, Func<int, double> score)
        {
            var best = candidates[0];
            var bestScore = score(best);
            for (var k = 1; k < candidates.Count; k++)
            {
                var s = score(candidates[k]);
                if (s > bestScore)
                {
                    best = candidates[k];
                    bestScore = s;
                }
            }
            return best;
        }
    }
}
