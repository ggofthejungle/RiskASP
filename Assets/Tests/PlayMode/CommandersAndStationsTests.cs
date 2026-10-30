using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Expansions;
using Map;
using NUnit.Framework;
using player;
using TurnPhases;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 2210 commanders and space stations: starting units, hiring (3 energy, one of each), building stations
// (5 energy, max 4), conquest (commanders die, stations captured), and 8-sided dice.
public class CommandersAndStationsTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;
    private Player _alice, _bob;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        GameManager.AutoPlaceStartingUnits = true; // start right at bidding
        UnityEngine.Debug.Log($"[TEST] start {NUnit.Framework.TestContext.CurrentContext.Test.Name}");
#if UNITY_EDITOR
        _config = AssetDatabase.LoadAssetAtPath<StartingPlayersConfiguration>(ConfigPath);
        var human = AssetDatabase.LoadAssetAtPath<PlayerConfiguration>(HumanConfigPath);
        var red = AssetDatabase.LoadAssetAtPath<PlayerColor>("Assets/ScriptableObjects/PlayerColors/Red.asset");
        var blue = AssetDatabase.LoadAssetAtPath<PlayerColor>("Assets/ScriptableObjects/PlayerColors/Blue.asset");
        _savedConfig = new List<PlayerCreationConfiguration>(_config.PlayersConfiguration);
        _config.PlayersConfiguration.Clear();
        _config.PlayersConfiguration.Add(new PlayerCreationConfiguration { PlayerConfiguration = human, PlayerName = "Alice", PlayerColor = red });
        _config.PlayersConfiguration.Add(new PlayerCreationConfiguration { PlayerConfiguration = human, PlayerName = "Bob", PlayerColor = blue });
#endif
        yield return SceneManager.LoadSceneAsync("Main");
        for (int i = 0; i < 300; i++)
        {
            _gm = GameManager.Instance;
            if (_gm != null && _gm.IsBidding) break;
            yield return null;
        }
        Assert.IsNotNull(_gm);
        _alice = _gm.Players.Single(p => p.Name == "Alice");
        _bob = _gm.Players.Single(p => p.Name == "Bob");
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Debug.Log($"[TEST] end {NUnit.Framework.TestContext.CurrentContext.Test.Name}");
        if (_config != null && _savedConfig != null)
        {
            _config.PlayersConfiguration.Clear();
            _config.PlayersConfiguration.AddRange(_savedConfig);
        }
    }

    // ---------- Starting units ----------

    [UnityTest]
    public IEnumerator EveryPlayer_StartsWithADiplomat_ALandCommander_AndOneSpaceStation()
    {
        foreach (var p in new[] { _alice, _bob })
        {
            Assert.IsTrue(p.HasCommanderInPlay(CommanderType.Diplomat), $"{p.Name} Diplomat");
            Assert.IsTrue(p.HasCommanderInPlay(CommanderType.LandCommander), $"{p.Name} Land Commander");
            Assert.IsFalse(p.HasCommanderInPlay(CommanderType.NuclearCommander), $"{p.Name} no Nuclear yet");
            Assert.AreEqual(2, p.Territories.Sum(t => t.CommandersList.Count), $"{p.Name}: 2 commanders");
            Assert.AreEqual(1, Purchases.StationsOf(p), $"{p.Name}: 1 station");
        }
        var all = Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude);
        Assert.AreEqual(2, all.Count(t => t.containsSpaceStation), "No other stations on the map");
        yield break;
    }

    // ---------- Hiring commanders ----------

    [UnityTest]
    public IEnumerator Hire_CostsThree_OnYourOwnTerritory()
    {
        StartAlicesTurn();
        var where = _alice.Territories.First();
        Assert.IsTrue(Hire(_alice, CommanderType.NuclearCommander, where));
        Assert.AreEqual(10 - Purchases.CommanderCost, _alice.GetEnergy());
        Assert.IsTrue(_alice.HasCommanderInPlay(CommanderType.NuclearCommander));
        Assert.IsTrue(where.CommandersList.Any(c => c.CommanderType == CommanderType.NuclearCommander));
        yield break;
    }

    [UnityTest]
    public IEnumerator Hire_IsRejected_WhenNotAllowed()
    {
        StartAlicesTurn();
        var mine = _alice.Territories.First();
        Assert.IsFalse(Hire(_alice, CommanderType.LandCommander, mine), "Already has one (one of each)");
        Assert.IsFalse(Hire(_alice, CommanderType.NuclearCommander, _bob.Territories.First()), "Not on an enemy territory");
        Assert.IsFalse(Hire(_alice, CommanderType.NavalCommander, mine), "No water on this map");
        Assert.IsFalse(Hire(_alice, CommanderType.SpaceCommander, mine), "No Moon on this map");
        _alice.SetEnergy(2);
        Assert.IsFalse(Hire(_alice, CommanderType.NuclearCommander, mine), "Needs 3 energy");
        Assert.IsFalse(_alice.HasCommanderInPlay(CommanderType.NuclearCommander));
        yield break;
    }

    [UnityTest]
    public IEnumerator Hire_OnlyBeforeYourFirstInvasion()
    {
        StartAlicesTurn();
        PlaceAllAndEndReinforce();
        Assert.IsNull(_gm.Purchases.WhyCantHire(_alice, CommanderType.NuclearCommander, _alice.Territories.First()),
            "Attack phase before the first invasion: OK");
        Invade();
        Assert.IsFalse(Hire(_alice, CommanderType.NuclearCommander, _alice.Territories.First()));
        yield break;
    }

    [UnityTest]
    public IEnumerator Hire_ADiplomatAgain_AfterYoursDied()
    {
        StartAlicesTurn();
        var t = _alice.Territories.First(x => x.CommandersList.Any(c => c.CommanderType == CommanderType.Diplomat));
        t.RemoveCommander(t.CommandersList.First(c => c.CommanderType == CommanderType.Diplomat));
        Assert.IsTrue(Hire(_alice, CommanderType.Diplomat, _alice.Territories.First()));
        yield break;
    }

    // ---------- Space stations ----------

    [UnityTest]
    public IEnumerator Build_CostsFive_MaxFour_OnePerTerritory()
    {
        StartAlicesTurn();
        _alice.SetEnergy(100);
        var free = _alice.Territories.Where(t => !t.containsSpaceStation).ToList();
        Assert.IsTrue(Build(_alice, free[0]));
        Assert.AreEqual(100 - Purchases.StationCost, _alice.GetEnergy());
        Assert.IsFalse(Build(_alice, free[0]), "One per territory");
        Assert.IsTrue(Build(_alice, free[1]));
        Assert.IsTrue(Build(_alice, free[2]));
        Assert.AreEqual(4, Purchases.StationsOf(_alice));
        Assert.IsFalse(Build(_alice, free[3]), "Max 4");
        Assert.IsFalse(Build(_alice, _bob.Territories.First(t => !t.containsSpaceStation)), "Not on enemy territory");
        yield break;
    }

    [UnityTest]
    public IEnumerator Station_GivesOneUnitAtTheStartOfYourTurn()
    {
        var station = _alice.Territories.First(t => t.containsSpaceStation);
        var before = station.Troops;
        StartAlicesTurn(); // Reinforce starts: +1 unit in each station territory
        Assert.AreEqual(before + 1, station.Troops);
        yield break;
    }

    // ---------- Conquest ----------

    [UnityTest]
    public IEnumerator Conquest_DestroysTheDefendersCommanders_AndCapturesTheStation()
    {
        StartAlicesTurn();
        PlaceAllAndEndReinforce();
        var (origin, target) = AdjacentPair();
        target.AddCommander(new Commander(CommanderType.NuclearCommander));
        if (!target.containsSpaceStation) target.AddSpaceStation();
        Conquer(origin, target);

        Assert.AreEqual(_alice, target.Owner);
        Assert.AreEqual(0, target.CommandersList.Count, "Bob's commanders there are destroyed");
        Assert.IsFalse(_bob.HasCommanderInPlay(CommanderType.NuclearCommander));
        Assert.IsTrue(target.containsSpaceStation, "Station captured");
        yield break;
    }

    [UnityTest]
    public IEnumerator Conquest_DestroysTheStation_IfTheAttackerAlreadyHasFour()
    {
        StartAlicesTurn();
        PlaceAllAndEndReinforce();
        var (origin, target) = AdjacentPair();
        foreach (var t in _alice.Territories.Where(t => !t.containsSpaceStation).Take(4 - Purchases.StationsOf(_alice)).ToList())
            t.AddSpaceStation();
        Assert.AreEqual(4, Purchases.StationsOf(_alice));
        if (!target.containsSpaceStation) target.AddSpaceStation();
        Conquer(origin, target);
        Assert.AreEqual(_alice, target.Owner);
        Assert.IsFalse(target.containsSpaceStation, "Destroyed, Alice already has 4");
        Assert.AreEqual(4, Purchases.StationsOf(_alice));
        yield break;
    }

    // ---------- Dice ----------

    [UnityTest]
    public IEnumerator Dice_CommandersAndStationsRollEightSided()
    {
        var t = _alice.Territories.First();
        foreach (var c in t.CommandersList.ToList()) t.RemoveCommander(c);
        if (t.containsSpaceStation) t.RemoveSpaceStation();

        Assert.AreEqual((0, 3), BattleSimulator.AttackDice(t, 3), "No commander: all d6");
        t.AddCommander(new Commander(CommanderType.Diplomat));
        Assert.AreEqual((0, 3), BattleSimulator.AttackDice(t, 3), "Diplomat never attacks with d8");
        t.AddCommander(new Commander(CommanderType.LandCommander));
        Assert.AreEqual((1, 2), BattleSimulator.AttackDice(t, 3), "Land Commander: one d8");
        t.AddCommander(new Commander(CommanderType.NuclearCommander));
        Assert.AreEqual((2, 1), BattleSimulator.AttackDice(t, 3), "Land + Nuclear: two d8");
        Assert.AreEqual((1, 0), BattleSimulator.AttackDice(t, 1), "Never more d8 than dice");

        Assert.AreEqual((2, 0), BattleSimulator.DefenseDice(t, 2), "3 commanders defend with d8");
        foreach (var c in t.CommandersList.ToList()) t.RemoveCommander(c);
        Assert.AreEqual((0, 2), BattleSimulator.DefenseDice(t, 2), "Plain units: d6");
        t.AddSpaceStation();
        Assert.AreEqual((2, 0), BattleSimulator.DefenseDice(t, 2), "Station: all defenders d8");
        yield break;
    }

    [UnityTest]
    public IEnumerator Defender_RollsAtMostTwoDice()
    {
        StartAlicesTurn();
        PlaceAllAndEndReinforce();
        var (origin, target) = AdjacentPair();
        origin.SetTroops(10);
        target.SetTroops(10);
        Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, 3)));
        var r = _gm.AttackPhase.LastAttackResult;
        Assert.AreEqual(3, r.AttackerRolls.Length);
        Assert.AreEqual(2, r.DefenderRolls.Length);
        Assert.AreEqual(2, r.AttackerLosses + r.DefenderLosses, "2 dice compared");
        yield break;
    }

    // ---------- Helpers ----------

    private bool Hire(Player p, CommanderType type, Territory where) =>
        _gm.HandlePlayerAction(new HireCommanderAction(p, _gm.Turn, type, where));

    private bool Build(Player p, Territory where) =>
        _gm.HandlePlayerAction(new BuildSpaceStationAction(p, _gm.Turn, where));

    private void StartAlicesTurn()
    {
        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(_gm.CurrentPlayer == _alice ? 1 : 0));
        TestGame.ChooseEarliest(_gm);
        Assert.AreEqual(_alice, _gm.CurrentPlayer);
        _alice.SetEnergy(10);
    }

    private void PlaceAllAndEndReinforce()
    {
        var p = _gm.CurrentPlayer;
        if (_gm.CurrentPhase is ReinforcePhase rp)
        {
            if (rp.RemainingTroopsToPlace > 0)
                _gm.HandlePlayerAction(new ReinforceAction(p, _gm.Turn, p.Territories.First(), rp.RemainingTroopsToPlace));
            if (_gm.CurrentPhase is ReinforcePhase)
                _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn));
        }
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);
    }

    private (Territory origin, Territory target) AdjacentPair()
    {
        var origin = _alice.Territories.First(t => t.HasAnyNeighbourEnemy());
        return (origin, origin.GetEnemyNeighbours().First());
    }

    private void Invade()
    {
        var (origin, target) = AdjacentPair();
        origin.SetTroops(Mathf.Max(origin.Troops, 4));
        Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, 3)));
    }

    private void Conquer(Territory origin, Territory target)
    {
        origin.SetTroops(80);
        target.SetTroops(1);
        for (int i = 0; i < 60 && target.Owner != _alice && _gm.AttackPhase.State == AttackState.Attacking; i++)
            _gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, 3));
    }
}
