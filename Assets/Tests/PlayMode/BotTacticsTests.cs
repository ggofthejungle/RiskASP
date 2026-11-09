using System.Linq;
using InvadeEarth.Rules;
using NUnit.Framework;

// The C# bot's MOD decisions (MM-2), on a 4-territory line: 0 - 1 - 2 - 3. Player 0 holds 0 and 1.
public class BotTacticsTests
{
    static BoardTerritory[] Line(int t0, int t1, int t2, int t3) => new[]
    {
        new BoardTerritory { Name = "A", OwnerId = 0, Troops = t0, Neighbours = new[] { 1 } },
        new BoardTerritory { Name = "B", OwnerId = 0, Troops = t1, Neighbours = new[] { 0, 2 } },
        new BoardTerritory { Name = "C", OwnerId = 1, Troops = t2, Neighbours = new[] { 1, 3 } },
        new BoardTerritory { Name = "D", OwnerId = 1, Troops = t3, Neighbours = new[] { 2 } },
    };

    [Test]
    public void Place_PutsEverythingOnTheOnlyBorder()
    {
        var placed = BotTactics.Place(Line(5, 2, 1, 4), 0, 3);
        Assert.AreEqual(1, placed.Count);
        Assert.AreEqual(1, placed[0].Territory);
        Assert.AreEqual(3, placed[0].Amount);
    }

    [Test]
    public void Attack_OnlyWithEnoughUnits_AndNeverACeaseFirePlayer()
    {
        Assert.IsNull(BotTactics.ChooseAttack(Line(5, 2, 1, 4), 0), "1 unit can leave against 1 defender");

        var attack = BotTactics.ChooseAttack(Line(5, 5, 1, 4), 0);
        Assert.IsNotNull(attack);
        Assert.AreEqual(1, attack.Value.From);
        Assert.AreEqual(2, attack.Value.To);
        Assert.AreEqual(3, attack.Value.Mods, "at most 3 dice");

        Assert.IsNull(BotTactics.ChooseAttack(Line(5, 5, 1, 4), 0, null, new[] { 1 }));
    }

    [Test]
    public void MoveIn_TakesEverythingWhenTheOriginIsSafe()
    {
        var map = Line(5, 6, 0, 4);
        map[2].OwnerId = 0; // just conquered
        Assert.AreEqual(5, BotTactics.MoveIn(map, 1, 2, 3));
    }

    [Test]
    public void Fortify_MovesTheBackStackTowardTheBorder()
    {
        var move = BotTactics.Fortify(Line(5, 2, 1, 4), 0);
        Assert.IsNotNull(move);
        Assert.AreEqual(0, move.Value.From);
        Assert.AreEqual(1, move.Value.To);
        Assert.AreEqual(4, move.Value.Amount);
        CollectionAssert.AreEqual(new[] { 1, 0, -1, -1 }, BotTactics.DistanceToBorder(Line(5, 2, 1, 4), 0));
    }

    [Test]
    public void SameBoard_SameAnswer()
    {
        var a = BotTactics.Place(Line(5, 2, 1, 4), 0, 7);
        var b = BotTactics.Place(Line(5, 2, 1, 4), 0, 7);
        CollectionAssert.AreEqual(a.Select(p => (p.Territory, p.Amount)).ToList(), b.Select(p => (p.Territory, p.Amount)).ToList());
    }
}
