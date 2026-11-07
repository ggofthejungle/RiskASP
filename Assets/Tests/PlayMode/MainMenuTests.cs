using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using player;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The Menu scene's front door: Play / Options / Credits / Quit, Discord,
// and the existing seat picker for single player and pass-and-play.
public class MainMenuTests
{
    const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";

    StartingPlayersConfiguration _config;
    List<PlayerCreationConfiguration> _saved;
    float _savedVolume;
    bool _hadVolume;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _hadVolume = PlayerPrefs.HasKey("ie.volume");
        _savedVolume = PlayerPrefs.GetFloat("ie.volume", 1f);
#if UNITY_EDITOR
        _config = AssetDatabase.LoadAssetAtPath<StartingPlayersConfiguration>(ConfigPath);
        Assert.IsNotNull(_config, "StartingPlayersConfiguration asset not found");
        _saved = new List<PlayerCreationConfiguration>(_config.PlayersConfiguration);
#endif
        yield return SceneManager.LoadSceneAsync("Menu");
        for (int i = 0; i < 30 && MainMenu.Instance == null; i++)
            yield return null;
    }

    [TearDown]
    public void TearDown()
    {
        MatchSession.Select(MatchKind.None);
        MatchSession.TryConsume(out _);
#if UNITY_EDITOR
        if (_config != null && _saved != null)
        {
            _config.PlayersConfiguration.Clear();
            _config.PlayersConfiguration.AddRange(_saved);
        }
#endif
        if (_hadVolume) GameSettings.SetVolume(_savedVolume);
        else PlayerPrefs.DeleteKey("ie.volume");
    }

    [Test]
    public void Session_ArmsOnce()
    {
        MatchSession.Select(MatchKind.SinglePlayer);
        MatchSession.ArmForNextLoad();
        Assert.IsTrue(MatchSession.TryConsume(out var kind));
        Assert.AreEqual(MatchKind.SinglePlayer, kind);
        Assert.IsFalse(MatchSession.TryConsume(out _));
    }

    [Test]
    public void Volume_RoundTrips_AndReachesTheListener()
    {
        GameSettings.SetVolume(0.35f);
        Assert.AreEqual(0.35f, GameSettings.Volume, 0.001f);
        Assert.AreEqual(0.35f, AudioListener.volume, 0.001f);
    }

    [UnityTest]
    public IEnumerator Menu_HasTheStandardEntries_AndDiscord()
    {
        Assert.IsNotNull(MainMenu.Instance, "Main menu should build itself on the Menu scene");
        Assert.AreEqual("Root", MainMenu.Instance.PageName);
        Assert.IsNotNull(GameObject.Find("Play"));
        Assert.IsNotNull(GameObject.Find("Options"));
        Assert.IsNotNull(GameObject.Find("Credits"));
        Assert.IsNotNull(GameObject.Find("Quit"));
        Assert.IsNotNull(GameObject.Find("Discord"));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SinglePlayer_ReusesTheSeatPicker_WithYouAndABot()
    {
        Click("Play");
        Click("Single Player");
        Assert.IsTrue(MainMenu.Instance.LobbyOpen);

        var lobby = Object.FindAnyObjectByType<UIPlayerCreationManager>();
        Assert.IsTrue(lobby.gameObject.activeInHierarchy);
        Assert.AreEqual(2, lobby.PlayerCount);
        Assert.AreEqual("You", _config.PlayersConfiguration[0].PlayerName);
        Assert.AreEqual("Human", _config.PlayersConfiguration[0].PlayerConfiguration.Name);
        Assert.AreEqual("Bot", _config.PlayersConfiguration[1].PlayerName);
        Assert.AreEqual("Dumb BOT", _config.PlayersConfiguration[1].PlayerConfiguration.Name);

        Click("Back");
        Assert.IsFalse(MainMenu.Instance.LobbyOpen);
        Assert.AreEqual("Play", MainMenu.Instance.PageName);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PassAndPlay_PrefillsTwoHumans()
    {
        Click("Play");
        Click("Pass and Play");
        Assert.AreEqual(2, _config.PlayersConfiguration.Count);
        Assert.AreEqual("Human", _config.PlayersConfiguration[0].PlayerConfiguration.Name);
        Assert.AreEqual("Human", _config.PlayersConfiguration[1].PlayerConfiguration.Name);
        Assert.AreEqual("Player 1", _config.PlayersConfiguration[0].PlayerName);
        Assert.AreEqual("Player 2", _config.PlayersConfiguration[1].PlayerName);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Multiplayer_ExplainsThatRoomsAreNotConnected()
    {
        Click("Play");
        Click("Multiplayer");
        Assert.IsNotNull(GameObject.Find("Host private room"));
        Assert.IsNotNull(GameObject.Find("Join code"));
        Click("Host private room");
        StringAssert.Contains("aren't connected", MainMenu.Instance.StatusText);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Options_AndCredits_AndQuitConfirm_Open()
    {
        Click("Options");
        Assert.IsNotNull(GameObject.Find("Master volume"));
        Assert.IsNotNull(GameObject.Find("Resolution"));
        Assert.IsNotNull(GameObject.Find("Fullscreen"));
        Click("Back");

        Click("Credits");
        var body = GameObject.Find("Body").GetComponent<TMPro.TMP_Text>();
        StringAssert.Contains("RiskASP", body.text);
        StringAssert.Contains("Nikolas Krecu", body.text);
        Click("Back");

        Click("Quit");
        Assert.IsNotNull(GameObject.Find("Cancel"));
        Click("Cancel");
        Assert.AreEqual("Root", MainMenu.Instance.PageName);
        yield return null;
    }

    static void Click(string name)
    {
        var go = GameObject.Find(name);
        Assert.IsNotNull(go, "No active object named " + name);
        var button = go.GetComponent<Button>();
        Assert.IsNotNull(button, name + " is not a button");
        button.onClick.Invoke();
    }
}
