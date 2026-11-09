using System.Linq;
using InvadeEarth.Rules;
using NUnit.Framework;

// Income, movement, the opening deal and a board that can fight, with no scene.
public class BoardRulesTests
{
    [Test]
    public void Energy_Is3_Until12Territories()
    {
        Assert.AreEqual(3, IncomeRules.EnergyIncome(0));
        Assert.AreEqual(3, IncomeRules.EnergyIncome(11));
        Assert.AreEqual(4, IncomeRules.EnergyIncome(12));
        Assert.AreEqual(5, IncomeRules.EnergyIncome(15));
        Assert.AreEqual(IncomeRules.StartingEnergy, 3);
    }

    [Test]
    public void TroopBonus_IsTheScoreChart_PlusContinents()
    {
        Assert.AreEqual(3, IncomeRules.TerritoryTroopBonus(8), "at least 3");
        Assert.AreEqual(4, IncomeRules.TerritoryTroopBonus(14));
        Assert.AreEqual(8, IncomeRules.TroopBonus(8, 5));
        Assert.AreEqual(9, IncomeRules.EnergyIncome(14, 5), "continent bonuses give energy too");
    }

    [Test]
    public void Move_LeavesOneBehind_AndBattleEmptiesAConqueredTerritory()
    {
        var blocked = TroopMovement.Move(1, 4, 1);
        Assert.IsFalse(blocked.Ok);

        var moved = TroopMovement.Move(5, 1, 3);
        Assert.IsTrue(moved.Ok);
        Assert.AreEqual(2, moved.FromTroops);
        Assert.AreEqual(4, moved.ToTroops);
        Assert.AreEqual(3, TroopMovement.MinMoveIn(3, 5));

        var won = TroopMovement.ApplyBattle(5, 1, 0, 0, 1, 0);
        Assert.IsTrue(won.Conquered);
        Assert.AreEqual(5, won.OriginTroops);
        Assert.AreEqual(0, won.TargetTroops);

        var modsFirst = TroopMovement.ApplyBattle(4, 3, 2, 1, 0, 3);
        Assert.AreEqual(1, modsFirst.ModLosses);
        Assert.AreEqual(3, modsFirst.OriginTroops);
        Assert.IsFalse(modsFirst.Conquered);
    }

    [Test]
    public void SameSeed_DealsTheSameOwnersAndTroops()
    {
        var a = OpeningDeal.Deal(12, 2, new SeededRandom(4));
        var b = OpeningDeal.Deal(12, 2, new SeededRandom(4));
        CollectionAssert.AreEqual(a.OwnerOf, b.OwnerOf);
        CollectionAssert.AreEqual(a.Troops, b.Troops);
        Assert.AreEqual(30, a.Troops.Where((t, i) => a.OwnerOf[i] == 0).Sum());
        Assert.AreEqual(30, a.Troops.Where((t, i) => a.OwnerOf[i] == 1).Sum());

        var other = OpeningDeal.Deal(12, 2, new SeededRandom(5));
        Assert.IsFalse(a.Troops.SequenceEqual(other.Troops) && a.OwnerOf.SequenceEqual(other.OwnerOf));
    }

    [Test]
    public void Board_Conquers_ThenMoveIn_ThenFortify()
    {
        var board = new MatchBoard
        {
            Energy = new[] { 3, 3 },
            Territories = new[]
            {
                new BoardTerritory { Name = "Alpha", OwnerId = 0, Troops = 6, Station = true, Neighbours = new[] { 1 } },
                new BoardTerritory { Name = "Beta", OwnerId = 1, Troops = 1, Neighbours = new[] { 0, 2 } },
                new BoardTerritory { Name = "Gamma", OwnerId = 0, Troops = 2, Neighbours = new[] { 1 } },
            },
        };
        board.BeginReinforce(0, 4);
        Assert.AreEqual(6, board.Energy[0], "3 starting + 3 income");
        Assert.AreEqual(7, board.Territories[0].Troops, "Station grants one unit");
        Assert.AreEqual(4, board.RemainingToPlace);
        Assert.IsTrue(board.Place(0, 4));
        Assert.AreEqual("Attack", board.Phase);
        Assert.AreEqual(11, board.Territories[0].Troops);

        Assert.IsTrue(board.Attack(0, 1, 3, 0, 0, new ScriptedRandom(6, 6, 6, 1)));
        Assert.IsTrue(board.LastConquered);
        Assert.AreEqual(0, board.Territories[1].OwnerId);
        Assert.AreEqual(0, board.Territories[1].Troops);
        Assert.AreEqual("MoveIn", board.Phase);
        Assert.IsFalse(board.MoveIn(2), "Three attacking MODs have to move at least 3");
        Assert.IsTrue(board.MoveIn(3));
        Assert.AreEqual(8, board.Territories[0].Troops);
        Assert.AreEqual(3, board.Territories[1].Troops);
        Assert.IsTrue(board.DefenderEliminated, "Beta was their only territory");

        board.EndAttack();
        Assert.IsFalse(board.Fortify(0, 2, 1), "Alpha does not touch Gamma");
        Assert.IsTrue(board.Fortify(1, 0, 1));
        Assert.AreEqual(9, board.Territories[0].Troops);
        Assert.AreEqual(2, board.Territories[1].Troops);
        Assert.AreEqual("Done", board.Phase);
    }

    [Test]
    public void BattleResult_IsVisibleToEveryPlayer()
    {
        var full = new MatchView
        {
            HasLastBattle = true,
            LastAttackerLosses = 1,
            LastDefenderLosses = 2,
            LastConquered = true,
            Players = new[] { new PlayerView { PlayerId = 0, CardIds = new[] { 7 } } },
        };
        var seen = HiddenInfo.ForPlayer(full, 1);
        Assert.IsTrue(seen.HasLastBattle);
        Assert.AreEqual(1, seen.LastAttackerLosses);
        Assert.AreEqual(2, seen.LastDefenderLosses);
        Assert.IsTrue(seen.LastConquered);
        Assert.IsNull(seen.Players[0].CardIds);
    }

    sealed class ScriptedRandom : IRandom
    {
        readonly int[] _values;
        int _i;
        public ScriptedRandom(params int[] values) => _values = values;
        public int Range(int minInclusive, int maxExclusive) => _values[_i++];
        public float Value => 0f;
    }
}
