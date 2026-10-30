using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Actions;
using Decisions;
using Map;
using NUnit.Framework;
using player;
using TurnPhases;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Turn owner vs input owner, and the PendingDecision / reaction-window plumbing in AttackPhase.
// No defense cards yet: tests force the window open with AttackPhase.ShouldOpenReactionWindow.
//
// Expected behaviour:
//  - With no decision open, the input owner is the turn owner.
//  - Declaring an attack with the window enabled opens a DefenseReaction decision for the defender,
//    BEFORE any dice are rolled. The turn owner does not change; the input owner becomes the defender.
//  - While it's open, only the defender may act, and only by answering with one of its options.
//    Anything else (from anyone) is rejected and changes nothing.
//  - Pass -> the roll happens, input returns to the attacker.
//  - Pass for rest of turn -> no more windows for that defender this turn.
public class InputOwnerTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;

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
        Assert.IsNotNull(_config, "StartingPlayersConfiguration asset not found");
        Assert.IsNotNull(human, "Human player config not found");

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
        Assert.IsNotNull(_gm, "GameManager did not load");
        Assert.IsTrue(_gm.IsBidding, "Game should start in bidding");
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Debug.Log($"[TEST] end {NUnit.Framework.TestContext.CurrentContext.Test.Name}");
        if (_gm != null && _gm.AttackPhase != null)
            _gm.AttackPhase.ShouldOpenReactionWindow = _ => false;
        if (_config != null && _savedConfig != null)
        {
            _config.PlayersConfiguration.Clear();
            _config.PlayersConfiguration.AddRange(_savedConfig);
        }
    }

    // ---------- Input owner ----------

    [UnityTest]
    public IEnumerator NoDecision_InputPlayer_IsTurnOwner()
    {
        Assert.IsNull(_gm.PendingDecision);
        Assert.AreEqual(_gm.CurrentPlayer, _gm.InputPlayer, "During bidding");

        GoToAttackPhase();
        Assert.IsNull(_gm.PendingDecision);
        Assert.AreEqual(_gm.CurrentPlayer, _gm.InputPlayer, "During Attack");
        yield break;
    }

    [UnityTest]
    public IEnumerator Attack_WithWindowEnabled_OpensDecisionForDefender_BeforeRolling()
    {
        var (attacker, attack) = DeclareAttackWithWindow();
        var defender = attack.Target.Owner;

        var d = _gm.PendingDecision;
        Assert.IsNotNull(d, "Decision should be open");
        Assert.AreEqual(DecisionKind.DefenseReaction, d.Kind);
        Assert.AreEqual(_gm.GetPlayerId(defender), d.PlayerId);
        CollectionAssert.AreEquivalent(new[] { DecisionOptions.Pass, DecisionOptions.PassForRestOfTurn }, d.Options);

        Assert.AreEqual(attacker, _gm.CurrentPlayer, "Turn owner unchanged");
        Assert.AreEqual(defender, _gm.InputPlayer, "Input owner is the defender");
        Assert.AreEqual(AttackState.AwaitingDefender, _gm.AttackPhase.State);
        Assert.AreSame(attack, _gm.AttackPhase.PendingAttack);
        Assert.AreEqual(0, _gm.AttackPhase.AttackResults.Count(), "No dice rolled yet");
        yield break;
    }

    // Required: an action from a player who isn't the one being asked is rejected.
    [UnityTest]
    public IEnumerator Response_FromPlayerNotBeingAsked_IsRejected()
    {
        var (attacker, attack) = DeclareAttackWithWindow();
        var originTroops = attack.Origin.Troops;
        var targetTroops = attack.Target.Troops;
        var decision = _gm.PendingDecision;

        LogAssert.Expect(LogType.Error, new Regex("not the input player"));
        var accepted = _gm.HandlePlayerAction(new DecisionResponseAction(attacker, _gm.Turn, DecisionOptions.Pass));

        Assert.IsFalse(accepted, "Attacker must not be able to answer the defender's decision");
        Assert.AreSame(decision, _gm.PendingDecision, "Decision still open");
        Assert.AreEqual(attack.Target.Owner, _gm.InputPlayer);
        Assert.AreEqual(0, _gm.AttackPhase.AttackResults.Count(), "No dice rolled");
        Assert.AreEqual(originTroops, attack.Origin.Troops);
        Assert.AreEqual(targetTroops, attack.Target.Troops);
        yield break;
    }

    [UnityTest]
    public IEnumerator TurnOwnerActions_WhileDecisionOpen_AreRejected()
    {
        var (attacker, attack) = DeclareAttackWithWindow();

        Assert.IsFalse(_gm.HandlePlayerAction(new EndPhaseAction(attacker, _gm.Turn)), "Can't end phase");
        Assert.IsFalse(_gm.HandlePlayerAction(
            new AttackAction(attacker, _gm.Turn, _gm.AttackPhase.AttackTurn, attack.Origin, attack.Target, 1)),
            "Can't attack again");
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);
        Assert.IsNotNull(_gm.PendingDecision);
        Assert.AreEqual(0, _gm.AttackPhase.AttackResults.Count());
        yield break;
    }

    [UnityTest]
    public IEnumerator Defender_InvalidOption_IsRejected()
    {
        var (_, attack) = DeclareAttackWithWindow();
        var defender = attack.Target.Owner;

        Assert.IsFalse(_gm.HandlePlayerAction(new DecisionResponseAction(defender, _gm.Turn, "Card:Nuke")));
        Assert.IsNotNull(_gm.PendingDecision, "Decision still open");
        yield break;
    }

    [UnityTest]
    public IEnumerator Response_WithNoDecisionOpen_IsRejected()
    {
        GoToAttackPhase();
        var p = _gm.CurrentPlayer;
        Assert.IsFalse(_gm.HandlePlayerAction(new DecisionResponseAction(p, _gm.Turn, DecisionOptions.Pass)));
        Assert.AreEqual(AttackState.Attacking, _gm.AttackPhase.State);
        yield break;
    }

    [UnityTest]
    public IEnumerator Defender_Pass_RollsDice_AndReturnsInputToAttacker()
    {
        var (attacker, attack) = DeclareAttackWithWindow();
        var defender = attack.Target.Owner;

        Assert.IsTrue(_gm.HandlePlayerAction(new DecisionResponseAction(defender, _gm.Turn, DecisionOptions.Pass)));

        Assert.IsNull(_gm.PendingDecision);
        Assert.AreEqual(1, _gm.AttackPhase.AttackResults.Count(), "Exactly one roll happened");
        Assert.AreEqual(attacker, _gm.InputPlayer, "Input back to the attacker");
        Assert.AreNotEqual(AttackState.AwaitingDefender, _gm.AttackPhase.State);
        Assert.IsFalse(_gm.AttackPhase.HasPassedForTurn(defender));
        yield break;
    }

    [UnityTest]
    public IEnumerator Defender_PassForRestOfTurn_SkipsLaterWindows_ThisTurnOnly()
    {
        var (attacker, attack) = DeclareAttackWithWindow();
        var defender = attack.Target.Owner;

        Assert.IsTrue(_gm.HandlePlayerAction(
            new DecisionResponseAction(defender, _gm.Turn, DecisionOptions.PassForRestOfTurn)));
        Assert.IsTrue(_gm.AttackPhase.HasPassedForTurn(defender));

        // Attack again (if still possible): no window, the roll happens straight away.
        var ap = _gm.AttackPhase;
        if (ap.State == AttackState.Fortifying)
            _gm.HandlePlayerAction(new AttackReinforceAction(attacker, _gm.Turn, ap.AttackTurn, attack, Mathf.Min(attack.Troops, attack.Origin.GetAvailableTroops())));
        var next = FindAttack(attacker);
        if (next != null)
        {
            var before = ap.AttackResults.Count();
            Assert.IsTrue(_gm.HandlePlayerAction(next));
            Assert.IsNull(_gm.PendingDecision, "No window after Pass for rest of turn");
            Assert.AreEqual(before + 1, ap.AttackResults.Count());
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator PhaseChange_ClosesAnyOpenDecision()
    {
        var (attacker, _) = DeclareAttackWithWindow();
        Assert.IsNotNull(_gm.PendingDecision);
        // Phase changes close decisions so a stale one can never block the next phase.
        _gm.NextTurnPhase();
        Assert.IsNull(_gm.PendingDecision);
        Assert.AreEqual(_gm.CurrentPlayer, _gm.InputPlayer);
        yield break;
    }

    // ---------- Serializable actions ----------

    [UnityTest]
    public IEnumerator AttackPhaseActions_RoundTripThroughJson()
    {
        GoToAttackPhase();
        var p = _gm.CurrentPlayer;
        var attack = FindAttack(p);
        Assert.IsNotNull(attack, "Need an attack to serialize");

        var actions = new PlayerAction[]
        {
            attack,
            new AttackReinforceAction(p, _gm.Turn, 4, attack, 2),
            new EndPhaseAction(p, _gm.Turn),
            new DecisionResponseAction(p, _gm.Turn, DecisionOptions.PassForRestOfTurn),
        };
        foreach (var action in actions)
        {
            var json = JsonUtility.ToJson(ActionCodec.ToData(action, _gm));
            var back = ActionCodec.FromData(JsonUtility.FromJson<ActionData>(json), _gm);
            Assert.AreEqual(action.GetType(), back.GetType(), json);
            Assert.AreSame(action.Player, back.Player, json);
            Assert.AreEqual(action.Turn, back.Turn, json);
            Assert.AreEqual(json, JsonUtility.ToJson(ActionCodec.ToData(back, _gm)), "Stable round trip");
        }

        var a2 = (AttackAction)ActionCodec.FromData(JsonUtility.FromJson<ActionData>(
            JsonUtility.ToJson(ActionCodec.ToData(attack, _gm))), _gm);
        Assert.AreSame(attack.Origin, a2.Origin);
        Assert.AreSame(attack.Target, a2.Target);
        Assert.AreEqual(attack.Troops, a2.Troops);
        Assert.AreEqual(attack.AttackTurn, a2.AttackTurn);
        yield break;
    }

    // ---------- Helpers ----------

    private void GoToAttackPhase()
    {
        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(0));
        TestGame.ChooseEarliest(_gm);
        var p = _gm.CurrentPlayer;
        Assert.IsInstanceOf<ReinforcePhase>(_gm.CurrentPhase);
        var rp = (ReinforcePhase)_gm.CurrentPhase;
        var border = Territories().Where(t => t.Owner == p && t.HasAnyNeighbourEnemy())
                                  .OrderByDescending(t => t.Troops).First();
        if (rp.RemainingTroopsToPlace > 0)
            _gm.HandlePlayerAction(new ReinforceAction(p, _gm.Turn, border, rp.RemainingTroopsToPlace));
        if (_gm.CurrentPhase is ReinforcePhase)
            _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn));
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);
    }

    private (Player attacker, AttackAction attack) DeclareAttackWithWindow()
    {
        GoToAttackPhase();
        _gm.AttackPhase.ShouldOpenReactionWindow = _ => true;
        var attacker = _gm.CurrentPlayer;
        var attack = FindAttack(attacker);
        Assert.IsNotNull(attack, "No legal attack found");
        Assert.IsTrue(_gm.HandlePlayerAction(attack), "Attack declaration should be accepted");
        return (attacker, attack);
    }

    private AttackAction FindAttack(Player p)
    {
        var origin = Territories().Where(t => t.Owner == p && t.HasAnyNeighbourEnemy() && t.GetAvailableTroops() >= 1)
                                  .OrderByDescending(t => t.Troops).FirstOrDefault();
        if (origin == null) return null;
        var target = origin.GetEnemyNeighbours().OrderBy(t => t.Troops).First();
        var troops = Mathf.Min(3, origin.GetAvailableTroops());
        return new AttackAction(p, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, troops);
    }

    private static IEnumerable<Territory> Territories() =>
        Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude);
}
