using System;
using System.Linq;

namespace InvadeEarth.Rules
{
    // One seat in this year's bid. Bid -1 means they have not bid yet.
    public sealed class YearSeat
    {
        public int PlayerId;
        public bool Alive;
        public int Energy;
        public int Bid = -1;
    }

    // Bidding, then the highest bidder downward choosing a turn position.
    // Dead players do not bid; they are appended after the chosen order.
    public sealed class YearTable
    {
        public bool Bidding;
        public bool Choosing;
        public int ActorId;
        public int NextChooser;
        public YearSeat[] Seats = Array.Empty<YearSeat>();
        public int[] ChooserIds = Array.Empty<int>();
        public int[] SeatOwner = Array.Empty<int>(); // index 0 = position 1; -1 = free
        public int[] DeadIds = Array.Empty<int>();
        public int[] TurnOrder;
    }

    public static class YearOrder
    {
        public static YearTable Begin(YearSeat[] queue)
        {
            var seats = queue.Select(Copy).ToArray();
            foreach (var seat in seats)
                seat.Bid = -1;
            return new YearTable
            {
                Bidding = true,
                ActorId = seats.First(s => s.Alive).PlayerId,
                Seats = seats,
            };
        }

        // Records this bidder's amount. When everyone alive has bid, pays the energy and
        // opens seat choice. Tie-break keys come from `random`, one per alive player, queue order.
        public static bool TryBid(YearTable table, int playerId, int amount, IRandom random, out YearTable next)
        {
            next = null;
            if (table == null || !table.Bidding || playerId != table.ActorId || random == null)
                return false;
            var actor = table.Seats.FirstOrDefault(s => s.PlayerId == playerId);
            if (actor == null || !actor.Alive || amount < 0 || amount > actor.Energy)
                return false;

            next = Copy(table);
            next.Seats.First(s => s.PlayerId == playerId).Bid = amount;

            var pending = next.Seats.FirstOrDefault(s => s.Alive && s.Bid < 0);
            if (pending != null)
            {
                next.ActorId = pending.PlayerId;
                return true;
            }

            var alive = next.Seats.Where(s => s.Alive).ToArray();
            var keys = alive.ToDictionary(s => s.PlayerId, _ => random.Range(0, int.MaxValue));
            var ordered = alive.OrderByDescending(s => s.Bid).ThenBy(s => keys[s.PlayerId]).ToArray();
            foreach (var seat in ordered)
                seat.Energy -= seat.Bid;

            next.Bidding = false;
            next.Choosing = true;
            next.ChooserIds = ordered.Select(s => s.PlayerId).ToArray();
            next.NextChooser = 0;
            next.SeatOwner = Enumerable.Repeat(-1, ordered.Length).ToArray();
            next.DeadIds = next.Seats.Where(s => !s.Alive).Select(s => s.PlayerId).ToArray();
            next.ActorId = next.ChooserIds[0];
            return true;
        }

        // `position` is 1-based. The caller is the chooser whose turn it is.
        public static bool TryChoose(YearTable table, int playerId, int position, out YearTable next)
        {
            next = null;
            if (table == null || !table.Choosing || table.NextChooser >= table.ChooserIds.Length)
                return false;
            if (playerId != table.ChooserIds[table.NextChooser])
                return false;
            if (position < 1 || position > table.SeatOwner.Length || table.SeatOwner[position - 1] != -1)
                return false;

            next = Copy(table);
            next.SeatOwner[position - 1] = playerId;
            next.NextChooser++;
            if (next.NextChooser < next.ChooserIds.Length)
            {
                next.ActorId = next.ChooserIds[next.NextChooser];
                return true;
            }

            next.Choosing = false;
            next.ActorId = next.SeatOwner[0];
            next.TurnOrder = next.SeatOwner.Concat(next.DeadIds).ToArray();
            return true;
        }

        static YearTable Copy(YearTable table) => new YearTable
        {
            Bidding = table.Bidding,
            Choosing = table.Choosing,
            ActorId = table.ActorId,
            NextChooser = table.NextChooser,
            Seats = table.Seats.Select(Copy).ToArray(),
            ChooserIds = (int[])table.ChooserIds.Clone(),
            SeatOwner = (int[])table.SeatOwner.Clone(),
            DeadIds = (int[])table.DeadIds.Clone(),
            TurnOrder = table.TurnOrder == null ? null : (int[])table.TurnOrder.Clone(),
        };

        static YearSeat Copy(YearSeat seat) => new YearSeat
        {
            PlayerId = seat.PlayerId,
            Alive = seat.Alive,
            Energy = seat.Energy,
            Bid = seat.Bid,
        };
    }
}
