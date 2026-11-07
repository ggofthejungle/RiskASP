using InvadeEarth.Rules;
using NUnit.Framework;

// Bot bids, with no scene.
public class BotBiddingTests
{
    [Test]
    public void KeepsTheReserve()
    {
        for (var energy = 0; energy <= BotBidding.Reserve; energy++)
            Assert.AreEqual(0, BotBidding.Choose(energy, 1, 5, new SeededRandom(energy)), $"energy {energy}");
    }

    [Test]
    public void NeverBidsIntoTheReserve()
    {
        var random = new SeededRandom(11);
        for (var energy = 0; energy <= 20; energy++)
        for (var year = 1; year <= 5; year++)
        {
            var bid = BotBiddingTests.Bid(energy, year, random);
            Assert.GreaterOrEqual(bid, 0);
            Assert.LessOrEqual(bid, System.Math.Max(0, energy - BotBidding.Reserve), $"energy {energy}, year {year}");
        }
    }

    [Test]
    public void LastYear_BidsAtLeastAsMuch()
    {
        for (var energy = 4; energy <= 20; energy++)
        {
            var normal = BotBidding.Choose(energy, 2, 5, new SeededRandom(3));
            var last = BotBidding.Choose(energy, 5, 5, new SeededRandom(3));
            Assert.GreaterOrEqual(last, normal, $"energy {energy}");
        }
    }

    static int Bid(int energy, int year, IRandom random) => BotBidding.Choose(energy, year, 5, random);
}
