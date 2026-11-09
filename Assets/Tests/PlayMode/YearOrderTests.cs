using System.Linq;
using InvadeEarth.Rules;
using NUnit.Framework;

// Bids and seat choice with no scene. The Unity year flow calls this same code.
public class YearOrderTests
{
    [Test]
    public void HigherBid_Pays_AndChoosesFirst()
    {
        var table = YearOrder.Begin(new[]
        {
            Seat(0, 5),
            Seat(1, 5),
        });
        Assert.IsTrue(YearOrder.TryBid(table, 0, 2, new ScriptedRandom(), out var mid));
        Assert.AreEqual(1, mid.ActorId);
        Assert.AreEqual(-1, table.Seats[0].Bid, "The caller's table is left unchanged");

        Assert.IsTrue(YearOrder.TryBid(mid, 1, 0, new ScriptedRandom(1, 2), out var done));
        CollectionAssert.AreEqual(new[] { 0, 1 }, done.ChooserIds);
        Assert.AreEqual(3, done.Seats.First(s => s.PlayerId == 0).Energy);
        Assert.AreEqual(5, done.Seats.First(s => s.PlayerId == 1).Energy);
        Assert.IsTrue(done.Choosing);
        Assert.AreEqual(0, done.ActorId);
    }

    [Test]
    public void EqualBids_HighDieChoosesFirst()
    {
        var start = BothBidOne();
        Assert.IsTrue(YearOrder.TryBid(start, 1, 1, new ScriptedRandom(2, 5), out var highSecond));
        CollectionAssert.AreEqual(new[] { 1, 0 }, highSecond.ChooserIds, "Player 1 rolled 5, player 0 rolled 2");

        Assert.IsTrue(YearOrder.TryBid(start, 1, 1, new ScriptedRandom(6, 3), out var highFirst));
        CollectionAssert.AreEqual(new[] { 0, 1 }, highFirst.ChooserIds);
    }

    [Test]
    public void Bid_OverEnergy_IsRejected()
    {
        var table = YearOrder.Begin(new[] { Seat(0, 2), Seat(1, 2) });
        Assert.IsFalse(YearOrder.TryBid(table, 0, 3, new ScriptedRandom(), out _));
        Assert.AreEqual(-1, table.Seats[0].Bid);
        Assert.IsTrue(table.Bidding);
    }

    [Test]
    public void ChoosingSecond_LeavesTheOtherSeat_ThenTurnOrder()
    {
        Assert.IsTrue(YearOrder.TryBid(BothBidOne(), 1, 1, new ScriptedRandom(1, 2), out var choosing));
        Assert.IsTrue(YearOrder.TryChoose(choosing, choosing.ActorId, 2, out var afterFirst));
        Assert.IsTrue(afterFirst.Choosing);
        Assert.IsFalse(YearOrder.TryChoose(afterFirst, afterFirst.ActorId, 2, out _), "Taken seat");
        Assert.IsTrue(YearOrder.TryChoose(afterFirst, afterFirst.ActorId, 1, out var done));
        CollectionAssert.AreEqual(new[] { afterFirst.ActorId, choosing.ActorId }, done.TurnOrder);
        Assert.IsFalse(done.Choosing);
    }

    [Test]
    public void DeadPlayer_DoesNotBid_AndFollowsTheChosenOrder()
    {
        var table = YearOrder.Begin(new[]
        {
            new YearSeat { PlayerId = 0, Alive = false, Energy = 4 },
            Seat(1, 6),
        });
        Assert.AreEqual(1, table.ActorId);
        Assert.IsTrue(YearOrder.TryBid(table, 1, 2, new ScriptedRandom(3), out var choosing));
        Assert.AreEqual(4, choosing.Seats.First(s => s.PlayerId == 0).Energy);
        CollectionAssert.AreEqual(new[] { 1 }, choosing.ChooserIds);
        CollectionAssert.AreEqual(new[] { 0 }, choosing.DeadIds);
        Assert.IsTrue(YearOrder.TryChoose(choosing, 1, 1, out var done));
        CollectionAssert.AreEqual(new[] { 1, 0 }, done.TurnOrder);
    }

    static YearTable BothBidOne()
    {
        var table = YearOrder.Begin(new[] { Seat(0, 5), Seat(1, 5) });
        Assert.IsTrue(YearOrder.TryBid(table, 0, 1, new ScriptedRandom(), out var mid));
        return mid;
    }

    static YearSeat Seat(int id, int energy) => new YearSeat { PlayerId = id, Alive = true, Energy = energy };

    sealed class ScriptedRandom : IRandom
    {
        readonly int[] _values;
        int _i;
        public ScriptedRandom(params int[] values) => _values = values;
        public int Range(int minInclusive, int maxExclusive) => _values[_i++];
        public float Value => 0f;
    }
}
