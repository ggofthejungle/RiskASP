using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using Decisions;
using Expansions;
using Map;
using NUnit.Framework;
using player;
using TurnPhases;
using UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// The command bar (hand, buy / hire / build, map targeting) and the hot-seat defender prompt,
// driven through the same calls the buttons and map clicks make.
public class CommandHudTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;
    private CommandHud _hud;
    private SelectionManager _sm;
    private Player _alice, _bob;

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
        _hud = _gm.CommandHud;
        Assert.IsNotNull(_hud, "CommandHud should be created");
        _sm = SelectionManager.Instance;
        _alice = _gm.Players.Single(p => p.Name == "Alice");
        _bob = _gm.Players.Single(p => p.Name == "Bob");
        yield return null;
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
    public IEnumerator Bar_IsHiddenWhileBidding_AndShownOnAHumanTurn()
    {
        Assert.IsFalse(_hud.IsBarVisible, "Bidding");
        yield return StartAlicesTurn();
        Assert.IsTrue(_hud.IsBarVisible);
        Assert.IsTrue(_hud.IsActionEnabled("buy" + CommanderType.Diplomat), "Alice has a Diplomat");
        Assert.IsTrue(_hud.IsActionEnabled("buy" + CommanderType.LandCommander));
        Assert.IsFalse(_hud.IsActionEnabled("buy" + CommanderType.NuclearCommander), "No Nuclear commander: no button");
        Assert.IsTrue(_hud.IsActionEnabled("hire" + CommanderType.NuclearCommander));
    }

    [UnityTest]
    public IEnumerator BuyButton_PutsACardInTheHand()
    {
        yield return StartAlicesTurn();
        var energy = _alice.GetEnergy();
        _hud.ClickBuy(CommanderType.LandCommander);
        Assert.IsTrue(_hud.IsConfirming, "Asks first");
        Assert.AreEqual(0, _alice.Cards.Count, "Nothing bought before confirming");
        _hud.Confirm();
        Assert.AreEqual(1, _alice.Cards.Count);
        Assert.AreEqual(energy - 1, _alice.GetEnergy());
        StringAssert.Contains("You got", _hud.Status);
    }

    [UnityTest]
    public IEnumerator Hire_ThenClickATerritory()
    {
        yield return StartAlicesTurn();
        _hud.ClickHire(CommanderType.NuclearCommander);
        _hud.Confirm();
        Assert.IsTrue(_sm.IsTargeting, "Waiting for a territory");
        var where = _alice.Territories.First();
        Click(where);
        Assert.IsFalse(_sm.IsTargeting);
        Assert.IsTrue(where.CommandersList.Any(c => c.CommanderType == CommanderType.NuclearCommander));
    }

    [UnityTest]
    public IEnumerator Build_ThenClickATerritory()
    {
        yield return StartAlicesTurn();
        _alice.SetEnergy(10);
        _hud.ClickBuild();
        _hud.Confirm();
        var where = _alice.Territories.First(t => !t.containsSpaceStation);
        Click(where);
        Assert.IsTrue(where.containsSpaceStation);
        Assert.AreEqual(5, _alice.GetEnergy());
    }

    [UnityTest]
    public IEnumerator Card_AssembleMods_ClickTheCardThenATerritory()
    {
        yield return StartAlicesTurn();
        var card = Give(_alice, CardType.AssembleModsL);
        var where = _alice.Territories.First();
        var before = where.Troops;
        _hud.ClickCard(card);
        Assert.IsTrue(_sm.IsTargeting);
        Click(where);
        Assert.AreEqual(before + 3, where.Troops);
        Assert.IsFalse(_alice.Cards.Contains(card));
    }

    [UnityTest]
    public IEnumerator Card_Reinforcements_NeedsThreeClicks()
    {
        yield return StartAlicesTurn();
        var card = Give(_alice, CardType.ReinforcementsL);
        var t = _alice.Territories.Take(3).ToArray();
        var before = t.Select(x => x.Troops).ToArray();
        _hud.ClickCard(card);
        Click(t[0]); Click(t[1]);
        Assert.IsTrue(_sm.IsTargeting, "Still waiting for the third");
        Click(t[2]);
        Assert.IsFalse(_sm.IsTargeting);
        for (int i = 0; i < 3; i++) Assert.AreEqual(before[i] + 1, t[i].Troops);
    }

    [UnityTest]
    public IEnumerator CancelTargeting_GivesTheMapBackToTheTurn()
    {
        yield return StartAlicesTurn();
        _hud.ClickBuild();
        _hud.Confirm();
        Assert.IsTrue(_sm.IsTargeting);
        _hud.CancelTargeting();
        Assert.IsFalse(_sm.IsTargeting);
        Assert.AreEqual(1, Purchases.StationsOf(_alice));
    }

    [UnityTest]
    public IEnumerator DefenderPrompt_HotSeat_CoverThenChoiceThenHandBack()
    {
        _gm.PassTheDeviceScreens = true; // hot-seat testing option
        yield return StartAlicesTurn();
        var card = Give(_bob, CardType.StealthModsL);
        var (origin, target) = Front();
        EndReinforce();
        Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, 3)));
        yield return null;

        Assert.AreEqual(CommandHud.PromptStage.Cover, _hud.Stage, "Pass the device to Bob first");
        Assert.IsFalse(_hud.IsBarVisible, "Alice's cards are hidden");
        _hud.ConfirmCover();
        yield return null;
        Assert.AreEqual(CommandHud.PromptStage.Choose, _hud.Stage);
        CollectionAssert.Contains(_hud.PromptOptions, CommandCards.OptionFor(card));
        StringAssert.Contains("Alice attacks", _hud.PromptText);

        Assert.IsTrue(_hud.Choose(DecisionOptions.Pass));
        yield return null;
        Assert.AreEqual(CommandHud.PromptStage.HandBack, _hud.Stage, "Pass the device back to Alice");
        Assert.AreEqual(1, _gm.AttackPhase.AttackResults.Count(), "The dice rolled");
        _hud.ConfirmCover();
        yield return null;
        Assert.AreEqual(CommandHud.PromptStage.Hidden, _hud.Stage);
    }

    [UnityTest]
    public IEnumerator DefenderPrompt_StaysOpen_ForASecondCard()
    {
        _gm.PassTheDeviceScreens = true;
        yield return StartAlicesTurn();
        var a = Give(_bob, CardType.StealthModsL);
        Give(_bob, CardType.StealthModsL);
        var (origin, target) = Front();
        EndReinforce();
        Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, 3)));
        yield return null;
        _hud.ConfirmCover();
        yield return null;
        Assert.IsTrue(_hud.Choose(CommandCards.OptionFor(a)));
        yield return null;
        Assert.AreEqual(CommandHud.PromptStage.Choose, _hud.Stage, "Bob is asked again (no hand-back in between)");
        Assert.IsNotNull(_gm.PendingDecision);
    }

    [UnityTest]
    public IEnumerator DefenderPrompt_Default_NoPassTheDeviceScreens()
    {
        Assert.IsFalse(_gm.PassTheDeviceScreens, "Off by default (the real game is online)");
        yield return StartAlicesTurn();
        Give(_bob, CardType.StealthModsL);
        var (origin, target) = Front();
        EndReinforce();
        Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, 3)));
        yield return null;
        Assert.AreEqual(CommandHud.PromptStage.Choose, _hud.Stage, "Straight to Bob's choice");
        Assert.IsTrue(_hud.Choose(DecisionOptions.Pass));
        yield return null;
        Assert.AreEqual(CommandHud.PromptStage.Hidden, _hud.Stage, "No hand-back screen");
    }

    [UnityTest]
    public IEnumerator Online_TheLocalPlayerOnlySeesTheirOwnHandAndPrompts()
    {
        _gm.LocalPlayer = _bob; // this device belongs to Bob
        yield return StartAlicesTurn();
        Assert.IsFalse(_hud.IsBarVisible, "Alice's turn: Bob doesn't see her hand or buttons");

        Give(_bob, CardType.StealthModsL);
        var (origin, target) = Front();
        EndReinforce();
        Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, 3)));
        yield return null;
        Assert.AreEqual(CommandHud.PromptStage.Choose, _hud.Stage, "Bob's device asks Bob");
        Assert.IsTrue(_hud.Choose(DecisionOptions.Pass));
        yield return null;
        Assert.AreEqual(CommandHud.PromptStage.Hidden, _hud.Stage);

        _gm.LocalPlayer = _alice; // ...and on Alice's device:
        Give(_bob, CardType.StealthModsL);
        if (target.Owner == _bob && _gm.AttackPhase.State == AttackState.Attacking)
        {
            Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, 3)));
            yield return null;
            Assert.AreEqual(CommandHud.PromptStage.Hidden, _hud.Stage, "Alice's device doesn't show Bob's prompt");
            StringAssert.Contains("Waiting for", _gm.TurnHud.InstructionText);
        }
    }

    // ---------- Helpers ----------

    private IEnumerator StartAlicesTurn()
    {
        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(_gm.CurrentPlayer == _alice ? 1 : 0));
        TestGame.ChooseEarliest(_gm);
        Assert.AreEqual(_alice, _gm.CurrentPlayer);
        _alice.SetEnergy(10);
        yield return null;
    }

    private void Click(Territory t)
    {
        var sel = _sm.TerritoryToSelectionMap[t];
        sel.OnClicked?.Invoke(sel);
    }

    private Card Give(Player p, CardType type)
    {
        var cr = CardRepository.Instance;
        var card = cr.AllCards.First(c => c.CardType == type && cr.CardsInDeck.Contains(c));
        cr.CardsInDeck.Remove(card);
        p.AddCard(card);
        return card;
    }

    private (Territory, Territory) Front()
    {
        var origin = _alice.Territories.First(t => t.GetEnemyNeighbours().Any());
        var target = origin.GetEnemyNeighbours().First();
        origin.SetTroops(10);
        target.SetTroops(5);
        return (origin, target);
    }

    private void EndReinforce()
    {
        if (_gm.CurrentPhase is ReinforcePhase)
            _gm.HandlePlayerAction(new EndPhaseAction(_alice, _gm.Turn));
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);
    }
}
