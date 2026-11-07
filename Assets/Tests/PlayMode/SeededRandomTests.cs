using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Map;
using NUnit.Framework;
using player;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The opening deal (who owns what, and how many units) follows the seed.
public class SeededRandomTests
{
    const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    StartingPlayersConfiguration _config;
    List<PlayerCreationConfiguration> _saved;

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
        yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        GameRandom.UseUnseeded();
#if UNITY_EDITOR
        if (_config != null && _saved != null)
        {
            _config.PlayersConfiguration.Clear();
            _config.PlayersConfiguration.AddRange(_saved);
        }
#endif
    }

    [UnityTest]
    public IEnumerator SameSeed_DealsTheSameMap_AndTheSameDiceStream()
    {
        GameRandom.UseSeed(7);
        var rolls = new int[8];
        for (int i = 0; i < rolls.Length; i++) rolls[i] = GameRandom.Current.Range(1, 9);
        GameRandom.UseSeed(7);
        for (int i = 0; i < rolls.Length; i++)
            Assert.AreEqual(rolls[i], GameRandom.Current.Range(1, 9));

        yield return LoadDeal(1);
        var first = _snap;
        yield return LoadDeal(2);
        Assert.AreNotEqual(first, _snap, "Two seeds should not deal the same map");
        yield return LoadDeal(1);
        Assert.AreEqual(first, _snap);
    }

    string _snap;

    IEnumerator LoadDeal(int seed)
    {
        GameRandom.UseSeed(seed);
        yield return SceneManager.LoadSceneAsync("Main");
        for (int i = 0; i < 300 && (GameManager.Instance == null || !GameManager.Instance.IsBidding); i++)
            yield return null;
        Assert.IsTrue(GameManager.Instance.IsBidding, "Game should reach bidding");
        _snap = string.Join("|", TerritoryRepository.Instance.Territories
            .OrderBy(t => t.Name)
            .Select(t => t.Name + "=" + t.Owner.Name + ":" + t.Troops));
    }
}
