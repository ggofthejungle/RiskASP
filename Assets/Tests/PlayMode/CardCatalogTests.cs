using System;
using Cards;
using InvadeEarth.Rules;
using NUnit.Framework;

// Card costs live in the rules catalog. The Unity card spec reads them from there.
public class CardCatalogTests
{
    [Test]
    public void KnownCards_HaveTheirCostAndTiming()
    {
        Assert.AreEqual(2, CardCatalog.For("CeaseFire").PowerUpCost);
        Assert.AreEqual(RuleTiming.OpponentInvades, CardCatalog.For("CeaseFire").Timing);
        Assert.AreEqual(3, CardCatalog.For("TheMother").PowerUpCost);
        Assert.AreEqual(0, CardCatalog.For("EnergyCrisis").PowerUpCost);
        Assert.AreEqual(RuleTiming.EndOfGame, CardCatalog.For("ColonyInfluenceL").Timing);
        Assert.AreEqual(4, CardCatalog.For("Armageddon").PowerUpCost);
        Assert.AreEqual(RuleTiming.BeforeFirstInvasion, CardCatalog.For("Armageddon").Timing);
        Assert.AreEqual(3, CardCatalog.For("AquaBrother").PowerUpCost);
        Assert.AreEqual(RuleTiming.BeforeFirstInvasion, CardCatalog.For("AquaBrother").Timing);
        Assert.AreEqual(RuleTiming.BeforeFirstInvasion, CardCatalog.For("NickyBoy").Timing);
        Assert.AreEqual(1, CardCatalog.BuyCost);
        Assert.AreEqual(4, CardCatalog.MaxBuysPerTurn);
    }

    [Test]
    public void EveryUnityCard_ReadsCostAndTimingFromTheCatalog()
    {
        foreach (CardType type in Enum.GetValues(typeof(CardType)))
        {
            var spec = CardSpec.For(type);
            var rule = CardCatalog.For(type.ToString());
            Assert.AreEqual(rule.PowerUpCost, spec.PowerUpCost, type.ToString());
            Assert.AreEqual(rule.Timing.ToString(), spec.Timing.ToString(), type.ToString());
        }
    }
}
