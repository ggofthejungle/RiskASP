using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Actions;
using Cards;
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

// Risk 2210 command cards (pilot): buy for 1 energy (max 4 per turn) from the deck of a commander you have in
// play, play on your turn before your first invasion, pay the power-up cost, discard (never reshuffled).
// Pilot cards: Energy Crisis (Diplomat) and Assemble MODs (Land). Naval/Space decks are out of play (land-only map).
public class CommandCardTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;
    private CardRepository _cr;
    private CommandCards _cc;
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
        _cr = CardRepository.Instance;
        _cc = _gm.CommandCards;
        Assert.IsNotNull(_cc);
        _alice = _gm.Players.Single(p => p.Name == "Alice");
        _bob = _gm.Players.Single(p => p.Name == "Bob");

        // SetupGame test-places commanders on one random territory; start every test from "nobody has any".
        foreach (var t in Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude))
            foreach (var c in t.CommandersList.ToList())
                t.RemoveCommander(c);
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

    // ---------- Buying ----------

    [UnityTest]
    public IEnumerator Buy_NeedsThatCommanderInPlay()
    {
        StartAlicesTurn();
        Assert.IsFalse(Buy(_alice, CommanderType.LandCommander), "No Land Commander yet");
        GiveCommander(_alice, CommanderType.LandCommander);
        Assert.IsTrue(Buy(_alice, CommanderType.LandCommander));
        Assert.IsFalse(Buy(_alice, CommanderType.NuclearCommander), "Land commander doesn't open the Nuclear deck");
        yield break;
    }

    [UnityTest]
    public IEnumerator Buy_CostsOneEnergy_AndGivesACardFromThatDeck()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.NuclearCommander);
        var energy = _alice.GetEnergy();
        var left = _cc.CardsLeftInDeck(CommanderType.NuclearCommander);

        Assert.IsTrue(Buy(_alice, CommanderType.NuclearCommander));

        Assert.AreEqual(energy - 1, _alice.GetEnergy());
        Assert.AreEqual(1, _alice.Cards.Count);
        Assert.AreEqual(CommanderType.NuclearCommander, _alice.Cards[0].Commander);
        Assert.AreEqual(left - 1, _cc.CardsLeftInDeck(CommanderType.NuclearCommander), "Card leaves the deck");
        yield break;
    }

    [UnityTest]
    public IEnumerator Buy_AtMostFourPerTurn_CountResetsNextTurn()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.NuclearCommander);
        for (int i = 0; i < 4; i++)
            Assert.IsTrue(Buy(_alice, CommanderType.NuclearCommander), $"buy {i + 1}");
        Assert.IsFalse(Buy(_alice, CommanderType.NuclearCommander), "5th buy");
        Assert.AreEqual(4, _alice.Cards.Count);

        PlayQuickTurn();
        Assert.AreEqual(_bob, _gm.CurrentPlayer);
        GiveCommander(_bob, CommanderType.NuclearCommander);
        _bob.SetEnergy(5);
        Assert.IsTrue(Buy(_bob, CommanderType.NuclearCommander), "New turn, new limit");
        yield break;
    }

    [UnityTest]
    public IEnumerator Buy_NeedsEnergy()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.LandCommander);
        _alice.SetEnergy(0);
        Assert.IsFalse(Buy(_alice, CommanderType.LandCommander));
        Assert.AreEqual(0, _alice.Cards.Count);
        yield break;
    }

    [UnityTest]
    public IEnumerator Buy_NavalAndSpaceDecks_AreOutOfPlay_OnALandOnlyMap()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.NavalCommander);
        GiveCommander(_alice, CommanderType.SpaceCommander);
        Assert.IsFalse(Buy(_alice, CommanderType.NavalCommander));
        Assert.IsFalse(Buy(_alice, CommanderType.SpaceCommander));
        yield break;
    }

    [UnityTest]
    public IEnumerator Buy_NotDuringBidding_NorAfterTheFirstInvasion()
    {
        GiveCommander(_alice, CommanderType.LandCommander);
        Assert.IsNotNull(_cc.WhyCantBuy(_alice, CommanderType.LandCommander), "Bidding");

        StartAlicesTurn();
        PlaceAllAndEndReinforce();
        Assert.IsNull(_cc.WhyCantBuy(_alice, CommanderType.LandCommander), "Attack phase, before the first invasion: OK");
        Invade();
        Assert.IsFalse(Buy(_alice, CommanderType.LandCommander), "After the first invasion");
        yield break;
    }

    // ---------- Playing: Energy Crisis (Diplomat) ----------

    [UnityTest]
    public IEnumerator EnergyCrisis_EndToEnd_PayCost_TakeEnergy_Discard()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.Diplomat);
        var card = Give(_alice, CardType.EnergyCrisis);
        _alice.SetEnergy(5);
        _bob.SetEnergy(4);

        Assert.IsTrue(_gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card)));

        Assert.AreEqual(5 - card.PowerUpCost + 1, _alice.GetEnergy(), "pays the power-up cost, gains 1 from Bob");
        Assert.AreEqual(3, _bob.GetEnergy());
        Assert.IsFalse(_alice.Cards.Contains(card), "Leaves the hand");
        Assert.IsTrue(_cc.DiscardPile.Contains(card), "Goes to the discard pile");
        Assert.IsFalse(_cr.CardsInDeck.Contains(card), "Never reshuffled into the deck");
        Assert.IsFalse(_gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card)), "Can't play it twice");
        yield break;
    }

    [UnityTest]
    public IEnumerator Play_NeedsTheCommanderStillInPlay()
    {
        StartAlicesTurn();
        var where = GiveCommander(_alice, CommanderType.Diplomat);
        var card = Give(_alice, CardType.EnergyCrisis);
        _alice.SetEnergy(5);
        foreach (var c in where.CommandersList.ToList()) where.RemoveCommander(c); // Diplomat died

        Assert.IsFalse(_gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card)));
        Assert.IsTrue(_alice.Cards.Contains(card), "Card is kept for later (rules: keep unplayed cards)");
        yield break;
    }

    [UnityTest]
    public IEnumerator Play_NeedsEnergyForThePowerUpCost()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.LandCommander);
        var card = Give(_alice, CardType.AssembleModsL); // power-up cost 1
        Assert.Greater(card.PowerUpCost, 0);
        _alice.SetEnergy(card.PowerUpCost - 1);
        Assert.IsFalse(_gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card, _alice.Territories.First())));
        Assert.IsTrue(_alice.Cards.Contains(card));
        yield break;
    }

    [UnityTest]
    public IEnumerator Play_BeforeFirstInvasionCard_IsRejectedAfterTheFirstInvasion()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.Diplomat);
        var card = Give(_alice, CardType.EnergyCrisis);
        _alice.SetEnergy(5);
        PlaceAllAndEndReinforce();
        Invade();
        Assert.IsFalse(_gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card)));
        yield break;
    }

    [UnityTest]
    public IEnumerator Play_OnSomeoneElsesTurn_IsRejected()
    {
        StartAlicesTurn();
        GiveCommander(_bob, CommanderType.Diplomat);
        var card = Give(_bob, CardType.EnergyCrisis);
        _bob.SetEnergy(5);
        LogAssert.Expect(LogType.Error, new Regex("not the input player"));
        Assert.IsFalse(_gm.HandlePlayerAction(new PlayCommandCardAction(_bob, _gm.Turn, card)));
        yield break;
    }

    [UnityTest]
    public IEnumerator Play_WhileFrequencyJammed_IsRejected()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.Diplomat);
        var card = Give(_alice, CardType.EnergyCrisis);
        _alice.SetEnergy(5);
        _alice.canPlayCommandCards = false;
        Assert.IsFalse(_gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card)));
        yield break;
    }

    // ---------- Playing: Assemble MODs (Land) ----------

    [UnityTest]
    public IEnumerator AssembleMods_Land_PutsThreeUnitsOnATerritoryYouOccupy()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.LandCommander);
        var card = Give(_alice, CardType.AssembleModsL);
        _alice.SetEnergy(5);
        var mine = _alice.Territories.First();
        var theirs = _bob.Territories.First();
        var before = mine.Troops;

        Assert.IsFalse(_gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card, theirs)), "Not on an enemy territory");
        Assert.IsTrue(_alice.Cards.Contains(card));

        Assert.IsTrue(_gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card, mine)));
        Assert.AreEqual(before + 3, mine.Troops);
        Assert.AreEqual(5 - card.PowerUpCost, _alice.GetEnergy());
        Assert.IsTrue(_cc.DiscardPile.Contains(card));
        yield break;
    }

    // ---------- Nuclear deck today ----------

    [UnityTest]
    public IEnumerator NuclearCards_CanBeBought_ButAreNotPlayableYet()
    {
        StartAlicesTurn();
        GiveCommander(_alice, CommanderType.NuclearCommander);
        Assert.IsTrue(Buy(_alice, CommanderType.NuclearCommander));
        var card = _alice.Cards.Single();
        Assert.AreEqual(CardTiming.NotImplemented, card.Timing, $"{card.Name}: no Nuclear card is implemented yet");
        Assert.IsFalse(_gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card)));
        yield break;
    }

    // ---------- Helpers ----------

    private bool Buy(Player p, CommanderType deck) =>
        _gm.HandlePlayerAction(new BuyCommandCardAction(p, _gm.Turn, deck));

    private Territory GiveCommander(Player p, CommanderType type)
    {
        var t = p.Territories.First();
        t.AddCommander(new Commander(type));
        return t;
    }

    // Puts a specific card in a player's hand (takes it out of the deck), for deterministic tests.
    private Card Give(Player p, CardType type)
    {
        var card = _cr.AllCards.Single(c => c.CardType == type);
        _cr.CardsInDeck.Remove(card);
        p.AddCard(card);
        return card;
    }

    private void StartAlicesTurn()
    {
        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(_gm.CurrentPlayer == _alice ? 1 : 0));
        TestGame.ChooseEarliest(_gm);
        Assert.AreEqual(_alice, _gm.CurrentPlayer);
        Assert.IsInstanceOf<ReinforcePhase>(_gm.CurrentPhase);
        _alice.SetEnergy(10);
    }

    private void PlaceAllAndEndReinforce()
    {
        var p = _gm.CurrentPlayer;
        if (_gm.CurrentPhase is ReinforcePhase rp)
        {
            var border = p.Territories.Where(t => t.HasAnyNeighbourEnemy()).OrderByDescending(t => t.Troops).First();
            if (rp.RemainingTroopsToPlace > 0)
                _gm.HandlePlayerAction(new ReinforceAction(p, _gm.Turn, border, rp.RemainingTroopsToPlace));
            if (_gm.CurrentPhase is ReinforcePhase)
                _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn));
        }
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);
    }

    private void Invade()
    {
        var p = _gm.CurrentPlayer;
        var origin = p.Territories.Where(t => t.HasAnyNeighbourEnemy() && t.GetAvailableTroops() >= 1)
                                  .OrderByDescending(t => t.Troops).First();
        var target = origin.GetEnemyNeighbours().First();
        Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(p, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target,
            Mathf.Min(3, origin.GetAvailableTroops()))));
        Assert.AreEqual(1, _gm.AttackPhase.AttackResults.Count());
    }

    private void PlayQuickTurn()
    {
        var p = _gm.CurrentPlayer;
        PlaceAllAndEndReinforce();
        _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn));
        _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn));
    }
}
