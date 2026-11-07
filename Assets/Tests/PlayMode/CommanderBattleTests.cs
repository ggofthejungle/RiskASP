using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Expansions;
using Map;
using NUnit.Framework;
using player;
using TurnPhases;
using TurnPhases.Selection;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Commanders in battle (George's rules, Oct 30):
//  - the attacker chooses which commanders join; each is a unit with its own die (Land/Nuclear d8, Diplomat d6), max 3 dice;
//  - losses hit the attacking MODs first: a commander dies only if no attacking MOD is left (e.g. attacking alone);
//  - winning attacking commanders move in; commanders can move along after a conquest and in Fortify;
//  - every commander death is announced (OnCommanderKilled + a red toast).
public class CommanderBattleTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;
    private Player _alice, _bob;
    private Territory _origin, _target;
    private readonly List<(Player owner, CommanderType type, Territory where, string how)> _killed = new();

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
        _killed.Clear();
        _gm.OnCommanderKilled += (o, t, w, h) => _killed.Add((o, t, w, h));

        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(_gm.CurrentPlayer == _alice ? 1 : 0));
        TestGame.ChooseEarliest(_gm);
        Assert.AreEqual(_alice, _gm.CurrentPlayer);
        var rp = (ReinforcePhase)_gm.CurrentPhase;
        _gm.HandlePlayerAction(new ReinforceAction(_alice, _gm.Turn, _alice.Territories.First(), rp.RemainingTroopsToPlace));
        if (_gm.CurrentPhase is ReinforcePhase) _gm.HandlePlayerAction(new EndPhaseAction(_alice, _gm.Turn));
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);

        // Known setup: no commanders/stations anywhere except what each test places.
        foreach (var t in Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude))
        {
            foreach (var c in t.CommandersList.ToList()) t.RemoveCommander(c);
            if (t.containsSpaceStation) t.RemoveSpaceStation();
        }
        _origin = _alice.Territories.First(t => t.HasAnyNeighbourEnemy());
        _target = _origin.GetEnemyNeighbours().First();
        _origin.SetTroops(10);
        _target.SetTroops(10);
        _killed.Clear();
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

    // ---------- Who rolls what ----------

    [UnityTest]
    public IEnumerator CommandersStayHome_UnlessChosen_AllDiceAreD6()
    {
        Add(_origin, CommanderType.LandCommander);
        Assert.IsTrue(Attack(3));
        var r = _gm.AttackPhase.LastAttackResult;
        Assert.AreEqual(3, r.AttackerRolls.Length);
        CollectionAssert.AreEqual(new[] { 6, 6, 6 }, r.AttackerDieSides, "Land Commander didn't join");
        Assert.IsTrue(Has(_origin, CommanderType.LandCommander));
        yield break;
    }

    [UnityTest]
    public IEnumerator LandAndNuclear_RollD8_Diplomat_RollsD6()
    {
        Add(_origin, CommanderType.LandCommander);
        Add(_origin, CommanderType.NuclearCommander);
        Add(_origin, CommanderType.Diplomat);

        Assert.IsTrue(Attack(1, CommanderType.LandCommander, CommanderType.NuclearCommander));
        var r = _gm.AttackPhase.LastAttackResult;
        Assert.AreEqual(3, r.AttackerRolls.Length, "1 unit + 2 commanders = 3 dice");
        Assert.AreEqual(2, r.AttackerD8Count, "Land + Nuclear: two d8");

        Assert.IsTrue(Attack(0, CommanderType.Diplomat));
        r = _gm.AttackPhase.LastAttackResult;
        Assert.AreEqual(1, r.AttackerRolls.Length, "Diplomat alone: one die");
        Assert.AreEqual(0, r.AttackerD8Count, "Diplomat attacks with d6");
        yield break;
    }

    [UnityTest]
    public IEnumerator NeverMoreThanThreeDice()
    {
        Add(_origin, CommanderType.LandCommander);
        Add(_origin, CommanderType.NuclearCommander);
        Assert.IsTrue(Attack(1, CommanderType.LandCommander, CommanderType.NuclearCommander));
        var r = _gm.AttackPhase.LastAttackResult;
        Assert.AreEqual(3, r.AttackerRolls.Length);
        Assert.AreEqual(2, r.AttackerD8Count, "The commanders' dice come first");
        Assert.IsFalse(Attack(3, CommanderType.LandCommander), "3 MODs + commander would be 4 dice");
        yield break;
    }

    [UnityTest]
    public IEnumerator Defenders_CommandersAndStation_RollD8()
    {
        Add(_target, CommanderType.Diplomat);
        Assert.IsTrue(Attack(3));
        Assert.AreEqual(1, _gm.AttackPhase.LastAttackResult.DefenderD8Count, "Defending commander: d8");
        _target.AddSpaceStation();
        Assert.IsTrue(Attack(3));
        Assert.AreEqual(2, _gm.AttackPhase.LastAttackResult.DefenderD8Count, "Station: all defenders d8");
        yield break;
    }

    [UnityTest]
    public IEnumerator Attack_WithACommanderThatIsNotThere_IsRejected()
    {
        Assert.IsFalse(Attack(2, CommanderType.NuclearCommander));
        Assert.AreEqual(0, _gm.AttackPhase.AttackResults.Count());
        yield break;
    }

    // ---------- Who dies ----------

    [UnityTest]
    public IEnumerator ArmyLoses_TheCommanderSurvives()
    {
        Add(_origin, CommanderType.LandCommander);
        _origin.SetTroops(30);
        _target.SetTroops(30);
        for (int i = 0; i < 12; i++)
            Assert.IsTrue(Attack(2, CommanderType.LandCommander));
        var lost = _gm.AttackPhase.AttackResults.Sum(r => r.AttackerLosses);
        Assert.Greater(lost, 0, "Some units were lost in 12 rolls");
        Assert.IsTrue(Has(_origin, CommanderType.LandCommander), "MODs took every loss");
        Assert.AreEqual(30 - lost, _origin.Troops);
        Assert.IsEmpty(_killed);
        yield break;
    }

    [UnityTest]
    public IEnumerator CommanderAlone_LosesAndDies_AndYouAreTold()
    {
        Add(_origin, CommanderType.LandCommander);
        _origin.SetTroops(2);
        _target.SetTroops(20);
        for (int i = 0; i < 60 && Has(_origin, CommanderType.LandCommander); i++)
            Assert.IsTrue(Attack(0, CommanderType.LandCommander));
        Assert.IsFalse(Has(_origin, CommanderType.LandCommander), "Lost a roll alone: killed");
        Assert.AreEqual(2, _origin.Troops, "No units were at risk");
        Assert.AreEqual(1, _killed.Count);
        Assert.AreEqual(_alice, _killed[0].owner);
        Assert.AreEqual(CommanderType.LandCommander, _killed[0].type);
        yield return null;
        StringAssert.Contains("LAND COMMANDER WAS KILLED", _gm.TurnHud.LastToast);
    }

    [UnityTest]
    public IEnumerator Conquest_DefendingCommandersDie_AndTheirOwnerIsTold()
    {
        Add(_target, CommanderType.Diplomat);
        _origin.SetTroops(80);
        _target.SetTroops(1);
        for (int i = 0; i < 60 && _target.Owner != _alice; i++)
            _gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, _origin, _target, 3));
        Assert.AreEqual(_alice, _target.Owner);
        Assert.IsTrue(_killed.Any(k => k.owner == _bob && k.type == CommanderType.Diplomat), "Bob is told");
        yield return null;
        StringAssert.Contains("Diplomat", _gm.TurnHud.LastToast);
        yield break;
    }

    // ---------- Who moves ----------

    [UnityTest]
    public IEnumerator WinningCommanders_MoveIn_OthersCanFollow()
    {
        Add(_origin, CommanderType.LandCommander);
        Add(_origin, CommanderType.Diplomat);
        _origin.SetTroops(80);
        _target.SetTroops(1);
        for (int i = 0; i < 60 && _target.Owner != _alice; i++)
            _gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, _origin, _target, 3,
                new[] { CommanderType.LandCommander }));
        Assert.AreEqual(_alice, _target.Owner);
        Assert.IsTrue(Has(_target, CommanderType.LandCommander), "The attacking Land Commander moved in");
        Assert.IsTrue(Has(_origin, CommanderType.Diplomat), "The Diplomat stayed (didn't attack)");

        Assert.AreEqual(AttackState.Fortifying, _gm.AttackPhase.State);
        var attack = _gm.AttackPhase.LastAttackResult.AttackAction;
        Assert.IsTrue(_gm.HandlePlayerAction(new AttackReinforceAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, attack, 5,
            new[] { CommanderType.Diplomat })));
        Assert.IsTrue(Has(_target, CommanderType.Diplomat), "Diplomat followed");
        Assert.AreEqual(5, _target.Troops);
        yield break;
    }

    [UnityTest]
    public IEnumerator Fortify_CommandersCanMoveAlong()
    {
        Add(_origin, CommanderType.NuclearCommander);
        Add(_origin, CommanderType.Diplomat);
        _gm.HandlePlayerAction(new EndPhaseAction(_alice, _gm.Turn)); // -> Fortify
        Assert.IsInstanceOf<FortifyPhase>(_gm.CurrentPhase);
        var to = _origin.GetFriendlyNeighbours().FirstOrDefault();
        if (to == null) { Assert.Inconclusive("No friendly neighbour on this map deal"); yield break; }

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("no LandCommander"));
        Assert.IsFalse(_gm.HandlePlayerAction(new FortifyAction(_alice, _gm.Turn, _origin, to, 2, new[] { CommanderType.LandCommander })),
            "No Land Commander there");
        Assert.IsTrue(_gm.HandlePlayerAction(new FortifyAction(_alice, _gm.Turn, _origin, to, 2, new[] { CommanderType.NuclearCommander })));
        Assert.IsTrue(Has(to, CommanderType.NuclearCommander));
        Assert.IsTrue(Has(_origin, CommanderType.Diplomat), "Diplomat stayed");
    }

    // ---------- The pickers ----------

    [UnityTest]
    public IEnumerator AttackPicker_AsksWhichCommandersJoin()
    {
        Add(_origin, CommanderType.LandCommander);
        Add(_origin, CommanderType.Diplomat);
        var sm = SelectionManager.Instance;
        Click(sm, _origin);
        Click(sm, _target);
        var hud = _gm.CommandHud;
        Assert.AreEqual(CommandHud.PickerMode.Attack, hud.Picker, "Asked before rolling");
        Assert.AreEqual(0, _gm.AttackPhase.AttackResults.Count());
        CollectionAssert.AreEquivalent(new[] { CommanderType.LandCommander }, hud.PickerCommanders, "d8 commanders pre-selected");

        hud.TogglePickerCommander(CommanderType.Diplomat);
        hud.SetPickerAmount(1);
        Assert.IsTrue(hud.ConfirmPicker());
        for (int i = 0; i < 300 && !_gm.AttackPhase.AttackResults.Any(); i++)
            yield return null; // the action reader may delay actions
        var r = _gm.AttackPhase.LastAttackResult;
        CollectionAssert.AreEquivalent(new[] { CommanderType.LandCommander, CommanderType.Diplomat }, r.AttackAction.Commanders);
        Assert.AreEqual(1, r.AttackAction.Troops);
        Assert.AreEqual(3, r.AttackerRolls.Length);
    }

    [UnityTest]
    public IEnumerator MoveInPicker_ChooseHowManyUnitsAndCommanders()
    {
        Add(_origin, CommanderType.Diplomat);
        _origin.SetTroops(20);
        _target.SetTroops(1);
        for (int i = 0; i < 60 && _target.Owner != _alice; i++)
            _gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, _origin, _target, 3));
        Assert.AreEqual(AttackState.Fortifying, _gm.AttackPhase.State);
        yield return null;
        var hud = _gm.CommandHud;
        Assert.AreEqual(CommandHud.PickerMode.MoveIn, hud.Picker);
        hud.SetPickerAmount(7);
        hud.TogglePickerCommander(CommanderType.Diplomat);
        var before = _origin.Troops;
        Assert.IsTrue(hud.ConfirmPicker());
        Assert.AreEqual(7, _target.Troops);
        Assert.AreEqual(before - 7, _origin.Troops);
        Assert.IsTrue(Has(_target, CommanderType.Diplomat));
        Assert.AreEqual(AttackState.Attacking, _gm.AttackPhase.State);
    }

    // ---------- Helpers ----------

    private bool Attack(int troops, params CommanderType[] commanders) =>
        _gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, _origin, _target, troops, commanders));

    private static void Add(Territory t, CommanderType type) => t.AddCommander(new Commander(type));
    private static bool Has(Territory t, CommanderType type) => t.CommandersList.Any(c => c.CommanderType == type);

    private static void Click(SelectionManager sm, Territory t)
    {
        var sel = sm.TerritoryToSelectionMap[t];
        sel.OnClicked?.Invoke(sel);
    }
}
