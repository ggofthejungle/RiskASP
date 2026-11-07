using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using Expansions;
using NUnit.Framework;
using player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Every player action survives a JSON round trip, and a bid is accepted only through HandlePlayerAction.
public class ActionCodecTests
{
    const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    StartingPlayersConfiguration _config;
    List<PlayerCreationConfiguration> _saved;
    GameManager _gm;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        GameManager.AutoPlaceStartingUnits = true;
#if UNITY_EDITOR
        _config = AssetDatabase.LoadAssetAtPath<StartingPlayersConfiguration>(ConfigPath);
        var human = AssetDatabase.LoadAssetAtPath<PlayerConfiguration>(HumanConfigPath);
        var red = AssetDatabase.LoadAssetAtPath<PlayerColor>("Assets/ScriptableObjects/PlayerColors/Red.asset");
        var blue = AssetDatabase.LoadAssetAtPath<PlayerColor>("Assets/ScriptableObjects/PlayerColors/Blue.asset");
        _saved = new List<PlayerCreationConfiguration>(_config.PlayersConfiguration);
        _config.PlayersConfiguration.Clear();
        _config.PlayersConfiguration.Add(new PlayerCreationConfiguration { PlayerConfiguration = human, PlayerName = "Alice", PlayerColor = red });
        _config.PlayersConfiguration.Add(new PlayerCreationConfiguration { PlayerConfiguration = human, PlayerName = "Bob", PlayerColor = blue });
#endif
        yield return SceneManager.LoadSceneAsync("Main");
        for (int i = 0; i < 300 && (GameManager.Instance == null || !GameManager.Instance.IsBidding); i++)
            yield return null;
        _gm = GameManager.Instance;
        Assert.IsTrue(_gm.IsBidding);
    }

    [TearDown]
    public void TearDown()
    {
#if UNITY_EDITOR
        if (_config != null && _saved != null)
        {
            _config.PlayersConfiguration.Clear();
            _config.PlayersConfiguration.AddRange(_saved);
        }
#endif
    }

    [UnityTest]
    public IEnumerator EveryAction_RoundTripsThroughJson()
    {
        var alice = _gm.Players.First(p => p.Name == "Alice");
        var bob = _gm.Players.First(p => p.Name == "Bob");
        var from = alice.Territories.First();
        var to = alice.Territories.Skip(1).First();
        var enemy = bob.Territories.First();
        var card = CardRepository.Instance.AllCards[0];
        var attack = new AttackAction(alice, _gm.Turn, 1, from, enemy, 2, new[] { CommanderType.LandCommander });

        var actions = new PlayerAction[]
        {
            new BidAction(alice, _gm.Turn, 2),
            new ChooseTurnPositionAction(alice, _gm.Turn, 1),
            new ReinforceAction(alice, _gm.Turn, from, 3),
            new FortifyAction(alice, _gm.Turn, from, to, 1, new[] { CommanderType.Diplomat }),
            new HireCommanderAction(alice, _gm.Turn, CommanderType.NuclearCommander, from),
            new BuildSpaceStationAction(alice, _gm.Turn, from),
            new BuyCommandCardAction(alice, _gm.Turn, CommanderType.LandCommander),
            new PlayCommandCardAction(alice, _gm.Turn, card, from, bob, CommanderType.Diplomat),
            attack,
            new AttackReinforceAction(alice, _gm.Turn, 1, attack, 2, new[] { CommanderType.Diplomat }),
            new EndPhaseAction(alice, _gm.Turn),
            new DecisionResponseAction(alice, _gm.Turn, "Pass"),
        };

        foreach (var action in actions)
        {
            var json = JsonUtility.ToJson(ActionCodec.ToData(action, _gm));
            var back = ActionCodec.FromData(JsonUtility.FromJson<ActionData>(json), _gm);
            Assert.AreEqual(action.GetType(), back.GetType(), json);
            Assert.AreSame(action.Player, back.Player, json);
            Assert.AreEqual(json, JsonUtility.ToJson(ActionCodec.ToData(back, _gm)), action.GetType().Name);
        }

        var played = (PlayCommandCardAction)ActionCodec.FromData(
            JsonUtility.FromJson<ActionData>(JsonUtility.ToJson(ActionCodec.ToData(actions[7], _gm))), _gm);
        Assert.AreEqual(card.Id, played.Card.Id);
        Assert.AreSame(from, played.Targets[0]);
        Assert.AreSame(bob, played.Targets[1]);
        Assert.AreEqual(CommanderType.Diplomat, played.Targets[2]);
        yield break;
    }

    [UnityTest]
    public IEnumerator BidAction_RejectsTooMuch_AndAcceptsALegalBid()
    {
        var bidder = _gm.CurrentPlayer;
        Assert.IsFalse(_gm.HandlePlayerAction(new BidAction(bidder, _gm.Turn, 99)));
        Assert.AreEqual(-1, bidder._energyBid);
        Assert.IsTrue(_gm.HandlePlayerAction(new BidAction(bidder, _gm.Turn, 1)));
        Assert.AreEqual(1, bidder._energyBid);
        Assert.AreNotSame(bidder, _gm.CurrentPlayer);
        yield break;
    }
}
