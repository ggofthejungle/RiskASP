using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Decisions;
using Expansions;
using Map;
using NUnit.Framework;
using player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// CC-5: year 1 is bid → choose turn order → then each player (in that order) places Diplomat,
// Land Commander and Space Station. Then the first reinforce turn starts.
public class StartingPlacementTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        GameManager.AutoPlaceStartingUnits = false;
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
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        GameManager.AutoPlaceStartingUnits = true;
        if (_config != null && _savedConfig != null)
        {
            _config.PlayersConfiguration.Clear();
            _config.PlayersConfiguration.AddRange(_savedConfig);
        }
    }

    private void BidAndChooseOrder()
    {
        Assert.IsTrue(_gm.IsBidding, "Year starts with bidding");
        Assert.IsFalse(_gm.IsPlacingStartingUnits);
        while (_gm.IsBidding)
            Assert.IsTrue(_gm.SubmitBid(0));
        while (_gm.IsChoosingOrder)
            Assert.IsTrue(_gm.ChooseTurnPosition(_gm.AvailableTurnPositions[0]));
    }

    [UnityTest]
    public IEnumerator BidAndOrderFirst_ThenThreePicksPerPlayer_ThenReinforce()
    {
        BidAndChooseOrder();

        Assert.IsTrue(_gm.IsPlacingStartingUnits, "After seats are chosen, starting units are placed");
        Assert.IsTrue(_gm.Players.All(p => p.Territories.All(t => t.CommandersList.Count == 0 && !t.containsSpaceStation)),
            "Nothing placed yet");

        var order = new List<(Player, string)>();
        var chosen = new Dictionary<(Player, string), Territory>();
        for (int i = 0; i < 10 && _gm.PendingDecision is { Kind: DecisionKind.PlaceStartingUnit } d; i++)
        {
            var p = _gm.GetPlayerById(d.PlayerId);
            Assert.AreEqual(p, _gm.CurrentPlayer);
            var where = p.Territories.Skip(i % 3).First();
            order.Add((p, d.Subject));
            chosen[(p, d.Subject)] = where;
            Assert.IsTrue(_gm.HandlePlayerAction(new DecisionResponseAction(p, _gm.Turn, where.Name)));
        }

        Assert.AreEqual(6, order.Count, "2 players x 3 units");
        CollectionAssert.AreEqual(new[] { "Diplomat", "LandCommander", "SpaceStation" }, order.Take(3).Select(o => o.Item2));
        Assert.AreEqual(order[0].Item1, order[2].Item1, "One player places all three, then the next");
        Assert.AreNotEqual(order[0].Item1, order[3].Item1);
        Assert.IsFalse(_gm.IsPlacingStartingUnits);
        Assert.IsFalse(_gm.IsBidding);
        Assert.IsInstanceOf<TurnPhases.ReinforcePhase>(_gm.CurrentPhase, "Then year-1 turns begin");

        foreach (var ((p, unit), t) in chosen)
        {
            if (unit == "SpaceStation") Assert.IsTrue(t.containsSpaceStation, $"{p.Name} station in {t.Name}");
            else Assert.IsTrue(t.CommandersList.Any(c => c.CommanderType.ToString() == unit), $"{p.Name} {unit} in {t.Name}");
        }
        foreach (var p in _gm.Players)
            Assert.AreEqual(1, Purchases.StationsOf(p));
        yield break;
    }

    [UnityTest]
    public IEnumerator YouCanOnlyPlaceOnYourOwnTerritory()
    {
        BidAndChooseOrder();
        var d = _gm.PendingDecision;
        var p = _gm.GetPlayerById(d.PlayerId);
        var enemy = _gm.Players.First(x => x != p).Territories.First();
        Assert.IsFalse(_gm.HandlePlayerAction(new DecisionResponseAction(p, _gm.Turn, enemy.Name)));
        Assert.AreSame(d, _gm.PendingDecision, "Still waiting for a valid pick");
        yield break;
    }

    [UnityTest]
    public IEnumerator Placement_ByClickingTheMap()
    {
        BidAndChooseOrder();
        var p = _gm.CurrentPlayer;
        var where = p.Territories.First();
        var sel = SelectionManager.Instance.TerritoryToSelectionMap[where];
        sel.OnClicked?.Invoke(sel);
        Assert.IsTrue(where.CommandersList.Any(c => c.CommanderType == CommanderType.Diplomat), "Clicked: Diplomat placed");
        Assert.AreEqual("LandCommander", _gm.PendingDecision.Subject, "Next: the Land Commander");
        yield return null;
        StringAssert.Contains("Land Commander", _gm.TurnHud.InstructionText);
        StringAssert.Contains("2 of 3", _gm.TurnHud.InstructionText);
    }

    [UnityTest]
    public IEnumerator TheAIPicksInstantly()
    {
        BidAndChooseOrder();
        var d = _gm.PendingDecision;
        var pick = DefenderAI.Choose(_gm, d);
        CollectionAssert.Contains(d.Options, pick);
        Assert.IsTrue(_gm.GetPlayerById(d.PlayerId).Territories.Any(t => t.Name == pick));
        yield break;
    }
}
