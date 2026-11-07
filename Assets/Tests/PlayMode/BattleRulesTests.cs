using InvadeEarth.Rules;
using NUnit.Framework;

// Dice and losses live in the rules assembly. These do not load a scene.
public class BattleRulesTests
{
    [Test]
    public void SameSeed_RollsTheSameDice()
    {
        var request = new BattleRequest { AttackMods = 2, AttackD6 = 2, DefenseD6 = 2, OriginTroops = 5, TargetTroops = 5 };
        var a = BattleRules.Resolve(request, new SeededRandom(3));
        var b = BattleRules.Resolve(request, new SeededRandom(3));
        CollectionAssert.AreEqual(a.AttackerRolls, b.AttackerRolls);
        CollectionAssert.AreEqual(a.DefenderRolls, b.DefenderRolls);
        CollectionAssert.AreEqual(a.AttackerDieSides, new[] { 6, 6 });
    }

    [Test]
    public void LossesHitModsBeforeTheCommander()
    {
        // Two attacker dice (1 MOD + 1 commander), both lose to a pair of sixes.
        var request = new BattleRequest
        {
            AttackMods = 1,
            AttackD6 = 2,
            DefenseD6 = 2,
            OriginTroops = 1,
            TargetTroops = 4,
        };
        var outcome = BattleRules.Resolve(request, new ScriptedRandom(1, 1, 6, 6));
        Assert.AreEqual(2, outcome.AttackerLosses);
        Assert.AreEqual(0, outcome.DefenderLosses);
        Assert.AreEqual(1, outcome.CommanderLosses, "The one MOD takes the first loss; the second kills the commander");
        Assert.AreEqual(0, outcome.RemainingAttackingTroops);
    }

    [Test]
    public void DefenderWinsTies()
    {
        var request = new BattleRequest { AttackMods = 1, AttackD6 = 1, DefenseD6 = 1, OriginTroops = 3, TargetTroops = 3 };
        var outcome = BattleRules.Resolve(request, new ScriptedRandom(4, 4));
        Assert.AreEqual(1, outcome.AttackerLosses);
        Assert.AreEqual(0, outcome.DefenderLosses);
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
