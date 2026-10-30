using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Cards;
using Expansions;
using Map;
using NUnit.Framework;
using player;
using TurnPhases;
using TurnPhases.Selection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Regression tests for what came in with the main -> dev merge (Oct 30, 2026):
// command cards / abilities, energy income per turn, and the arrow between close neighbours.
public class MergeRegressionTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;
    private CardRepository _cr;
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
        Assert.IsNotNull(_cr);
        _alice = _gm.Players.Single(p => p.Name == "Alice");
        _bob = _gm.Players.Single(p => p.Name == "Bob");
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

    // ---------- Cards: deck and ability wiring ----------

    [UnityTest]
    public IEnumerator Cards_OneCardPerCardType_EachRunsTheAbilityWithItsName()
    {
        var types = (CardType[])Enum.GetValues(typeof(CardType));
        Assert.AreEqual(types.Length, _cr.AllCards.Length, "One card per CardType");
        CollectionAssert.AreEquivalent(types, _cr.AllCards.Select(c => c.CardType));

        foreach (var card in _cr.AllCards)
        {
            var name = card.CardType.ToString();
            var ability = card.AbilityName;
            Assert.IsTrue(name == ability || (name.StartsWith(ability) && new[] { "D", "L", "Na", "S" }.Contains(name.Substring(ability.Length))),
                $"Card {name} runs ability {ability}");
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator Cards_AreInTheRightCommandersDeck()
    {
        // Spot checks at every group boundary in CardType.cs (these were wrong before).
        Assert.AreEqual(CommanderType.Diplomat, Commander(CardType.TerritorialStation));
        Assert.AreEqual(CommanderType.LandCommander, Commander(CardType.AssembleModsL));
        Assert.AreEqual(CommanderType.LandCommander, Commander(CardType.StealthStation));
        Assert.AreEqual(CommanderType.NavalCommander, Commander(CardType.AssembleModsNa));
        Assert.AreEqual(CommanderType.NavalCommander, Commander(CardType.WaterDeathTrap));
        Assert.AreEqual(CommanderType.NuclearCommander, Commander(CardType.AquaBrother));
        Assert.AreEqual(CommanderType.NuclearCommander, Commander(CardType.TheMother));
        Assert.AreEqual(CommanderType.SpaceCommander, Commander(CardType.AssembleModsS));
        Assert.AreEqual(CommanderType.SpaceCommander, Commander(CardType.StealthModsS));

        Assert.AreEqual(7, _cr.DiplomatCommander.Cards.Count, "Diplomat");
        Assert.AreEqual(8, _cr.LandCommander.Cards.Count, "Land");
        Assert.AreEqual(7, _cr.NavalCommander.Cards.Count, "Naval");
        Assert.AreEqual(11, _cr.NuclearCommander.Cards.Count, "Nuclear");
        Assert.AreEqual(8, _cr.SpaceCommander.Cards.Count, "Space");
        Assert.IsTrue(_cr.LandCommander.Cards.Contains(Card(CardType.LandDeathTrap)));
        Assert.IsTrue(_cr.NavalCommander.Cards.Contains(Card(CardType.WaterDeathTrap)));
        yield break;
    }

    // ---------- Card abilities ----------

    [UnityTest]
    public IEnumerator EnergyCrisis_TakesOneEnergyFromEachOpponent()
    {
        _alice.SetEnergy(2);
        _bob.SetEnergy(4);
        Card(CardType.EnergyCrisis).UseAbility(_alice);
        Assert.AreEqual(3, _alice.GetEnergy());
        Assert.AreEqual(3, _bob.GetEnergy());
        yield break;
    }

    [UnityTest]
    public IEnumerator EnergyCrisis_NeverPushesAnOpponentBelowZero()
    {
        _alice.SetEnergy(2);
        _bob.SetEnergy(0);
        Card(CardType.EnergyCrisis).UseAbility(_alice);
        Assert.AreEqual(0, _bob.GetEnergy(), "Bob had nothing to give");
        Assert.AreEqual(2, _alice.GetEnergy(), "So Alice gains nothing");
        yield break;
    }

    [UnityTest]
    public IEnumerator DeathTraps_DestroyHalfRoundedUp_KeepAtLeastOne_AndUpdateTheMapLabel()
    {
        var t = _alice.Territories.First();
        var changes = 0;
        t.OnTroopsChanged += (_, _) => changes++;

        foreach (var type in new[] { CardType.LandDeathTrap, CardType.WaterDeathTrap, CardType.OrbitalMines })
        {
            t.SetTroops(5);
            Card(type).UseAbility(t);
            Assert.AreEqual(2, t.Troops, $"{type}: 5 units, destroy 3 -> 2 left");

            t.SetTroops(4);
            Card(type).UseAbility(t);
            Assert.AreEqual(2, t.Troops, $"{type}: 4 units, destroy 2 -> 2 left");

            t.SetTroops(1);
            Card(type).UseAbility(t);
            Assert.AreEqual(1, t.Troops, $"{type}: never below 1");
        }
        Assert.Greater(changes, 0, "SetTroops must raise OnTroopsChanged (the map label listens to it)");
        yield break;
    }

    [UnityTest]
    public IEnumerator Evacuation_MovesAllUnits_NoneLost()
    {
        var from = _alice.Territories.First();
        var to = _alice.Territories.Skip(1).First();
        from.SetTroops(4);
        to.SetTroops(2);
        Card(CardType.Evacuation).UseAbility(from, to);
        Assert.AreEqual(6, from.Troops + to.Troops, "No units lost");
        Assert.AreEqual(6, to.Troops);
        yield break;
    }

    [UnityTest]
    public IEnumerator StationCards_PlaceASpaceStation()
    {
        var a = _alice.Territories.First(t => !t.containsSpaceStation);
        Card(CardType.TerritorialStation).UseAbility(a);
        Assert.IsTrue(a.containsSpaceStation, "Territorial Station");
        var b = _bob.Territories.First(t => !t.containsSpaceStation);
        Card(CardType.StealthStation).UseAbility(b);
        Assert.IsTrue(b.containsSpaceStation, "Stealth Station");
        yield break;
    }

    [UnityTest]
    public IEnumerator NewGame_HasNoSpaceStations_ExceptTheOneTestPlacedInSetup()
    {
        // Territory.prefab had containsSpaceStation saved as true, so EVERY territory had a station and
        // every player got +1 troop per territory each turn.
        var withStation = Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude).Count(t => t.containsSpaceStation);
        Assert.LessOrEqual(withStation, 1, "Only GameManager.SetupGame's test station");
        yield break;
    }

    [UnityTest]
    public IEnumerator EveryCard_CanBeUsedWithItsTargets_WithoutThrowing()
    {
        foreach (var card in _cr.AllCards)
        {
            var args = card.CardType switch
            {
                CardType.Evacuation => new object[] { _alice.Territories.First(), _alice.Territories.Skip(1).First() },
                _ => Args(card)
            };
            Assert.DoesNotThrow(() => card.UseAbility(args), card.CardType.ToString());
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator FrequencyJam_WearsOffWhenTheTurnChanges()
    {
        BidSoFirstIs(_alice);
        Assert.AreEqual(_alice, _gm.CurrentPlayer);
        Card(CardType.FrequencyJamL).UseAbility(_bob);
        Assert.IsFalse(_bob.canPlayCommandCards, "Jammed during Alice's turn");

        PlayQuickTurn();
        Assert.AreEqual(_bob, _gm.CurrentPlayer);
        Assert.IsTrue(_bob.canPlayCommandCards, "Jam is over once Alice's turn ends");
        yield break;
    }

    // ---------- Energy income (from main) ----------

    [UnityTest]
    public IEnumerator EnergyIncome_Is3_WithFewerThan12Territories_ElseTerritoriesDividedBy3()
    {
        // Give Bob all but 10 of Alice's territories.
        foreach (var t in _alice.Territories.Skip(10).ToList())
            t.SetOwner(_bob, t.Troops);
        Assert.AreEqual(10, _alice.GetTerritoryCount());
        var bobTerritories = _bob.GetTerritoryCount();
        Assert.GreaterOrEqual(bobTerritories, 12);

        BidSoFirstIs(_alice); // Alice bids 1, Bob 0
        Assert.AreEqual(3 - 1 + 3, _alice.GetEnergy(), "10 territories -> +3");

        PlayQuickTurn();
        Assert.AreEqual(_bob, _gm.CurrentPlayer);
        Assert.AreEqual(3 - 0 + bobTerritories / 3, _bob.GetEnergy(), $"{bobTerritories} territories -> +{bobTerritories / 3}");
        yield break;
    }

    // ---------- Arrow ----------

    [UnityTest]
    public IEnumerator Arrow_IsVisible_EvenBetweenTheClosestNeighboursOnTheMap()
    {
        BidSoFirstIs(_alice);
        PlaceAllAndEndReinforce();
        var territories = Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude);
        var (origin, target) = territories
            .SelectMany(t => t.NeighbourTerritories.Select(n => (t, n)))
            .OrderBy(pair => Vector2.Distance(pair.t.transform.position, pair.n.transform.position))
            .First();
        Debug.Log($"Closest neighbours: {origin.Name} - {target.Name}, " +
                  $"{Vector2.Distance(origin.transform.position, target.transform.position):F2} units");

        if (origin.Owner != _alice) origin.SetOwner(_alice, 5); else origin.SetTroops(5);
        if (target.Owner != _bob) target.SetOwner(_bob, 2);

        var sm = SelectionManager.Instance;
        var originSel = sm.TerritoryToSelectionMap[origin];
        var targetSel = sm.TerritoryToSelectionMap[target];
        originSel.OnClicked?.Invoke(originSel);
        targetSel.SendMessage("OnMouseEnter");
        yield return null;
        Assert.IsTrue(_gm.SelectionArrow.IsVisible, "Arrow must show for close neighbours too");
        Assert.AreEqual(origin, _gm.SelectionArrow.ShownFrom);
        Assert.AreEqual(target, _gm.SelectionArrow.ShownTo);
        targetSel.SendMessage("OnMouseExit");
    }

    // ---------- Helpers ----------

    private Card Card(CardType type) => _cr.AllCards.Single(c => c.CardType == type);
    private CommanderType Commander(CardType type) => Card(type).Commander;

    // Default targets for an ability, by its parameter types.
    private object[] Args(Card card)
    {
        var parameters = typeof(Abilities).GetMethod(card.AbilityName).GetParameters();
        return parameters.Select(p =>
            p.ParameterType == typeof(Player) ? (object)_bob :
            p.ParameterType == typeof(Territory) ? _bob.Territories.First() :
            p.ParameterType == typeof(Commander) ? new Commander(CommanderType.Diplomat) :
            null).ToArray();
    }

    private void BidSoFirstIs(Player first)
    {
        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(_gm.CurrentPlayer == first ? 1 : 0));
        Assert.AreEqual(first, _gm.CurrentPlayer);
    }

    private void PlaceAllAndEndReinforce()
    {
        var p = _gm.CurrentPlayer;
        if (_gm.CurrentPhase is ReinforcePhase rp)
        {
            if (rp.RemainingTroopsToPlace > 0)
                _gm.HandlePlayerAction(new ReinforceAction(p, _gm.Turn, p.Territories.First(), rp.RemainingTroopsToPlace));
            if (_gm.CurrentPhase is ReinforcePhase)
                _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn));
        }
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);
    }

    private void PlayQuickTurn()
    {
        var p = _gm.CurrentPlayer;
        PlaceAllAndEndReinforce();
        _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn)); // end Attack
        _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn)); // end Fortify
    }
}
