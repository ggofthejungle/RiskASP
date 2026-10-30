using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Map;
using NUnit.Framework;
using player;
using TurnPhases;
using TurnPhases.Selection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Turn banner (whose turn / which phase / what to do) and the attack arrow.
public class TurnHudTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
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
        Assert.IsNotNull(_gm.TurnHud, "TurnHud should be created");
        Assert.IsNotNull(_gm.SelectionArrow, "SelectionArrow should be created");
        yield return null; // let the HUD refresh once
    }

    [TearDown]
    public void TearDown()
    {
        if (_config != null && _savedConfig != null)
        {
            _config.PlayersConfiguration.Clear();
            _config.PlayersConfiguration.AddRange(_savedConfig);
        }
    }

    [UnityTest]
    public IEnumerator Banner_DuringBidding_ShowsBidderAndHowToBid()
    {
        var p = _gm.CurrentPlayer;
        StringAssert.Contains(p.Name.ToUpperInvariant(), _gm.TurnHud.TitleText);
        StringAssert.Contains("bid", _gm.TurnHud.InstructionText.ToLowerInvariant());
        Assert.IsFalse(_gm.SelectionArrow.IsVisible, "No arrow during bidding");
        yield break;
    }

    [UnityTest]
    public IEnumerator Banner_WhileChoosingTurnOrder_AsksTheHighestBidderToChoose()
    {
        Assert.IsTrue(_gm.SubmitBid(1));
        Assert.IsTrue(_gm.SubmitBid(0));
        yield return null;
        Assert.IsTrue(_gm.IsChoosingOrder);
        var chooser = _gm.CurrentPlayer;
        StringAssert.Contains($"{chooser.Name.ToUpperInvariant()} CHOOSES", _gm.TurnHud.TitleText);
        StringAssert.Contains("choose when you play", _gm.TurnHud.InstructionText);
        StringAssert.Contains("1st", _gm.TurnHud.InstructionText);
        Assert.IsFalse(_gm.SelectionArrow.IsVisible);
    }

    [UnityTest]
    public IEnumerator Banner_FollowsTurnAndPhase_AndSplashShowsForHumanTurn()
    {
        BidAll();
        yield return null;
        var p = _gm.CurrentPlayer;
        Assert.AreEqual($"{p.Name.ToUpperInvariant()}'S TURN", _gm.TurnHud.TitleText);
        Assert.IsTrue(_gm.TurnHud.IsSplashVisible, "Turn splash should show for a human turn");
        StringAssert.Contains(p.Name.ToUpperInvariant(), _gm.TurnHud.SplashTitleText);
        StringAssert.Contains("place troops", _gm.TurnHud.InstructionText);

        PlaceAllAndEndReinforce();
        yield return null;
        StringAssert.Contains("attack <b>from</b>", _gm.TurnHud.InstructionText);
    }

    [UnityTest]
    public IEnumerator Arrow_FromSelectedTerritory_ToHoveredEnemy()
    {
        BidAll();
        PlaceAllAndEndReinforce();
        var p = _gm.CurrentPlayer;
        var sm = SelectionManager.Instance;
        Assert.IsInstanceOf<AttackSelectionPhase>(sm.CurrentSelectionPhase);

        var origin = Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude)
            .Where(t => t.Owner == p && t.HasAnyNeighbourEnemy() && t.GetAvailableTroops() >= 1)
            .OrderByDescending(t => t.Troops).First();
        var target = origin.GetEnemyNeighbours().First();

        // 1st click: my territory
        var originSel = sm.TerritoryToSelectionMap[origin];
        originSel.OnClicked?.Invoke(originSel);
        yield return null;
        Assert.AreEqual(origin, ((AttackSelectionPhase)sm.CurrentSelectionPhase).From);
        StringAssert.Contains("Attacking from", _gm.TurnHud.InstructionText);

        // Hover the enemy: arrow snaps origin -> target
        var targetSel = sm.TerritoryToSelectionMap[target];
        targetSel.SendMessage("OnMouseEnter");
        yield return null;
        Assert.IsTrue(_gm.SelectionArrow.IsVisible, "Arrow visible while aiming");
        Assert.AreEqual(origin, _gm.SelectionArrow.ShownFrom);
        Assert.AreEqual(target, _gm.SelectionArrow.ShownTo);
        targetSel.SendMessage("OnMouseExit");

        // Cancel by clicking the origin again: arrow goes away
        originSel.OnClicked?.Invoke(originSel);
        yield return null;
        Assert.IsNull(((AttackSelectionPhase)sm.CurrentSelectionPhase).From);
    }

    [UnityTest]
    public IEnumerator Arrow_ShowsDeclaredAttack_WhileDefenderDecides()
    {
        BidAll();
        PlaceAllAndEndReinforce();
        var p = _gm.CurrentPlayer;
        var origin = Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude)
            .Where(t => t.Owner == p && t.HasAnyNeighbourEnemy() && t.GetAvailableTroops() >= 1)
            .OrderByDescending(t => t.Troops).First();
        var target = origin.GetEnemyNeighbours().First();
        _gm.AttackPhase.ShouldOpenReactionWindow = _ => true;
        try
        {
            Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(p, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target,
                Mathf.Min(3, origin.GetAvailableTroops()))));
            yield return null;
            Assert.IsTrue(_gm.SelectionArrow.IsVisible);
            Assert.AreEqual(origin, _gm.SelectionArrow.ShownFrom);
            Assert.AreEqual(target, _gm.SelectionArrow.ShownTo);
            StringAssert.Contains("Waiting for", _gm.TurnHud.InstructionText);
        }
        finally
        {
            _gm.AttackPhase.ShouldOpenReactionWindow = _ => false;
        }
    }

    private void BidAll()
    {
        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(0));
        TestGame.ChooseEarliest(_gm);
    }

    private void PlaceAllAndEndReinforce()
    {
        var p = _gm.CurrentPlayer;
        if (_gm.CurrentPhase is ReinforcePhase rp)
        {
            var border = Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude)
                .Where(t => t.Owner == p && t.HasAnyNeighbourEnemy()).OrderByDescending(t => t.Troops).First();
            if (rp.RemainingTroopsToPlace > 0)
                _gm.HandlePlayerAction(new ReinforceAction(p, _gm.Turn, border, rp.RemainingTroopsToPlace));
            if (_gm.CurrentPhase is ReinforcePhase)
                _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn));
        }
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);
    }
}
