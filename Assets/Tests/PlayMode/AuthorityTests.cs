using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using NUnit.Framework;
using player;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The authority applies a wire action and shows each device only its own hand.
public class AuthorityTests
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
    public IEnumerator SubmitBid_OnTheAuthority_AndHideTheOtherHand()
    {
        var alice = _gm.Players.First(p => p.Name == "Alice");
        var bob = _gm.Players.First(p => p.Name == "Bob");
        var card = CardRepository.Instance.AllCards[0];
        bob.AddCard(card);

        var auth = new GameAuthority(_gm);
        var bidder = _gm.CurrentPlayer;
        var data = ActionCodec.ToData(new BidAction(bidder, _gm.Turn, 0), _gm);
        Assert.IsTrue(auth.Submit(data, out var error), error);
        Assert.AreEqual(0, bidder._energyBid);

        var aliceId = _gm.GetPlayerId(alice);
        var bobId = _gm.GetPlayerId(bob);
        var asAlice = auth.ViewFor(aliceId);
        var bobOnAlice = asAlice.Players.First(p => p.PlayerId == bobId);
        Assert.AreEqual(1, bobOnAlice.HandCount);
        Assert.IsNull(bobOnAlice.CardIds, "Alice must not see Bob's cards");

        var bobSelf = auth.ViewFor(bobId).Players.First(p => p.PlayerId == bobId);
        Assert.AreEqual(card.Id, bobSelf.CardIds[0]);
        yield break;
    }
}
