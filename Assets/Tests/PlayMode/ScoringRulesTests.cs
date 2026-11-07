using InvadeEarth.Rules;
using NUnit.Framework;

// Score arithmetic with no scene. One survivor wins even if a dead player has more energy.
public class ScoringRulesTests
{
    [Test]
    public void DeadPlayer_ScoresZero()
    {
        var line = new ScoreLine { Alive = false, Territories = 9, ContinentBonus = 5, ColonyCards = 2, Energy = 40 };
        Assert.AreEqual(0, ScoringRules.Score(line));
    }

    [Test]
    public void Score_IsTerritories_PlusContinents_PlusThreePerColonyCard()
    {
        var line = new ScoreLine { Alive = true, Territories = 8, ContinentBonus = 5, ColonyCards = 2 };
        Assert.AreEqual(8 + 5 + 6, ScoringRules.Score(line));
    }

    [Test]
    public void SoleSurvivor_RanksFirst()
    {
        var lines = new[]
        {
            new ScoreLine { Alive = false, Energy = 30, Troops = 40 },
            new ScoreLine { Alive = true, Territories = 1 },
            new ScoreLine { Alive = false, Energy = 1 },
        };
        Assert.AreEqual(1, ScoringRules.Rank(lines)[0]);
    }

    [Test]
    public void Tie_GoesToMoreEnergy_ThenMoreUnits()
    {
        var same = new[]
        {
            new ScoreLine { Alive = true, Territories = 4, Energy = 2, Troops = 10 },
            new ScoreLine { Alive = true, Territories = 4, Energy = 5, Troops = 1 },
        };
        Assert.AreEqual(1, ScoringRules.Rank(same)[0], "More energy");

        var energyTied = new[]
        {
            new ScoreLine { Alive = true, Territories = 4, Energy = 3, Troops = 4 },
            new ScoreLine { Alive = true, Territories = 4, Energy = 3, Troops = 9 },
        };
        Assert.AreEqual(1, ScoringRules.Rank(energyTied)[0], "More units");
    }
}
