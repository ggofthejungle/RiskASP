using InvadeEarth.Rules;
using NUnit.Framework;

// Commander dice, with no scene. The battle simulator calls this same split.
public class DiceRulesTests
{
    [Test]
        public void TerrainCommanders_AttackWithD8_DiplomatDoesNot()
    {
        Assert.IsTrue(DiceRules.AttacksWithD8("LandCommander", "Land", "Land"));
        Assert.IsTrue(DiceRules.AttacksWithD8("LandCommander", "Land", "Sea"));
        Assert.IsFalse(DiceRules.AttacksWithD8("LandCommander", "Sea", "Sea"));
        Assert.IsTrue(DiceRules.AttacksWithD8("NuclearCommander", "Sea", "Moon"));
        Assert.IsFalse(DiceRules.AttacksWithD8("Diplomat", "Land", "Land"));
        Assert.IsTrue(DiceRules.AttacksWithD8("NavalCommander", "Land", "Sea"));
        Assert.IsTrue(DiceRules.AttacksWithD8("NavalCommander", "Sea", "Land"));
        Assert.IsFalse(DiceRules.AttacksWithD8("NavalCommander", "Land", "Land"));
        Assert.IsTrue(DiceRules.AttacksWithD8("SpaceCommander", "Moon", "Land"));
        Assert.IsFalse(DiceRules.AttacksWithD8("SpaceCommander", "Land", "Land"));
    }

    [Test]
    public void AttackDice_D8CommandersTakeSlots_ThenModsAreD6()
    {
        var (d8, d6) = DiceRules.Split(3, 2);
        Assert.AreEqual(2, d8);
        Assert.AreEqual(1, d6);

        var diplomat = DiceRules.Split(1, 0);
        Assert.AreEqual(0, diplomat.d8);
        Assert.AreEqual(1, diplomat.d6);
    }

    [Test]
    public void Defense_StationRollsAllD8_OtherwiseOneD8PerCommander()
    {
        var station = DiceRules.DefenseDice(2, spaceStation: true, commandersPresent: 0);
        Assert.AreEqual(2, station.d8);
        Assert.AreEqual(0, station.d6);

        var oneCommander = DiceRules.DefenseDice(2, spaceStation: false, commandersPresent: 1);
        Assert.AreEqual(1, oneCommander.d8);
        Assert.AreEqual(1, oneCommander.d6);
    }
}
