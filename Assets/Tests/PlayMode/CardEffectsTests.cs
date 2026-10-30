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
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

// CC-4: the remaining playable command cards (land-only map) and end-of-game scoring.
public class CardEffectsTests
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
        _alice = _gm.Players.Single(p => p.Name == "Alice");
        _bob = _gm.Players.Single(p => p.Name == "Bob");
        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(_gm.CurrentPlayer == _alice ? 1 : 0));
        TestGame.ChooseEarliest(_gm);
        Assert.AreEqual(_alice, _gm.CurrentPlayer);
        _alice.SetEnergy(20);
        _alice.Territories.First().AddCommander(new Commander(CommanderType.NuclearCommander)); // Diplomat + Land from the start
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

    // ---------- Diplomat ----------

    [UnityTest]
    public IEnumerator TerritorialStation_PlacesAStation_OnAFreeTerritoryYouOccupy()
    {
        var card = Give(_alice, CardType.TerritorialStation);
        var withStation = _alice.Territories.First(t => t.containsSpaceStation);
        var free = _alice.Territories.First(t => !t.containsSpaceStation);
        Assert.IsFalse(Play(card, withStation), "Already has a station");
        Assert.IsFalse(Play(card, _bob.Territories.First(t => !t.containsSpaceStation)), "Not Bob's territory");
        Assert.IsTrue(Play(card, free));
        Assert.IsTrue(free.containsSpaceStation);
        Assert.AreEqual(19, _alice.GetEnergy(), "cost 1");
        yield break;
    }

    [UnityTest]
    public IEnumerator ModReduction_OpponentsLoseFour_YouLoseTwo()
    {
        var card = Give(_alice, CardType.ModReduction);
        var bobBefore = _bob.GetTroopsCountInTerritories();
        var aliceBefore = _alice.GetTroopsCountInTerritories();
        Assert.IsTrue(Play(card));
        Assert.AreEqual(bobBefore - 4, _bob.GetTroopsCountInTerritories());
        Assert.AreEqual(aliceBefore - 2, _alice.GetTroopsCountInTerritories());
        Assert.IsTrue(_bob.Territories.All(t => t.Troops >= 1));
        Assert.AreEqual(18, _alice.GetEnergy(), "cost 2");
        yield break;
    }

    [UnityTest]
    public IEnumerator Colony_Influence_CantBePlayed_ItCountsAtTheEnd()
    {
        var card = Give(_alice, CardType.ColonyInfluenceD);
        Assert.IsFalse(Play(card));
        Assert.IsTrue(_alice.Cards.Contains(card));
        yield break;
    }

    [UnityTest]
    public IEnumerator Redeployment_GivesAnExtraFortifyMove_OnlyInFortify()
    {
        var card = Give(_alice, CardType.Redeployment);
        Assert.IsFalse(Play(card), "Not before attacking");
        EndPhase(); // Reinforce (places the rest randomly)
        EndPhase(); // Attack -> Fortify
        Assert.IsInstanceOf<FortifyPhase>(_gm.CurrentPhase);
        Assert.IsTrue(Play(card));

        var (from, to) = FortifyPair();
        Assert.IsTrue(_gm.HandlePlayerAction(new FortifyAction(_alice, _gm.Turn, from, to, 1)));
        Assert.IsInstanceOf<FortifyPhase>(_gm.CurrentPhase, "Still fortifying: extra move");
        (from, to) = FortifyPair();
        Assert.IsTrue(_gm.HandlePlayerAction(new FortifyAction(_alice, _gm.Turn, from, to, 1)));
        Assert.AreEqual(_bob, _gm.CurrentPlayer, "Second move ends the turn");
        yield break;
    }

    // ---------- Land ----------

    [UnityTest]
    public IEnumerator Reinforcements_OneUnitOnThreeDifferentTerritories()
    {
        var card = Give(_alice, CardType.ReinforcementsL);
        var t = _alice.Territories.Take(3).ToArray();
        var before = t.Select(x => x.Troops).ToArray();
        Assert.IsFalse(Play(card, t[0], t[0], t[1]), "Must be different");
        Assert.IsTrue(Play(card, t[0], t[1], t[2]));
        for (int i = 0; i < 3; i++) Assert.AreEqual(before[i] + 1, t[i].Troops);
        yield break;
    }

    [UnityTest]
    public IEnumerator FrequencyJam_Card_BlocksTheChosenOpponent_UntilTheTurnEnds()
    {
        var card = Give(_alice, CardType.FrequencyJamL);
        Assert.IsFalse(Play(card, _alice), "Not yourself");
        Assert.IsTrue(Play(card, _bob));
        Assert.IsFalse(_bob.canPlayCommandCards);
        EndPhase(); EndPhase(); EndPhase();
        Assert.AreEqual(_bob, _gm.CurrentPlayer);
        Assert.IsTrue(_bob.canPlayCommandCards);
        yield break;
    }

    [UnityTest]
    public IEnumerator ScoutForces_FiveUnitsAppear_WhenYouOccupyTheScoutedTerritory()
    {
        var card = Give(_alice, CardType.ScoutForces);
        Assert.IsTrue(Play(card));
        var scouted = _gm.CommandCards.ScoutedBy(_alice).Single();
        Assert.AreNotEqual(_alice, scouted.Owner);
        scouted.SetOwner(_alice, 2);
        Assert.AreEqual(2 + CommandCards.ScoutForcesUnits, scouted.Troops);
        Assert.IsEmpty(_gm.CommandCards.ScoutedBy(_alice), "Used up");
        yield break;
    }

    // ---------- Nuclear ----------

    [UnityTest]
    public IEnumerator RocketStrike_DestroysOneToSixUnits_OnAnOpponentsTerritory()
    {
        var card = Give(_alice, CardType.RocketStrikeLand);
        var target = _bob.Territories.First();
        target.SetTroops(10);
        Assert.IsFalse(Play(card, _alice.Territories.First()), "Not your own territory");
        Assert.IsTrue(Play(card, target));
        var roll = _cr.Abilities.LastRoll;
        Assert.That(roll, Is.InRange(1, 6));
        Assert.AreEqual(10 - roll, target.Troops);
        Assert.AreEqual(18, _alice.GetEnergy(), "cost 2");
        yield break;
    }

    [UnityTest]
    public IEnumerator ScatterBomb_HalvesOpponentsUnits_OnThreeRandomTerritories()
    {
        foreach (var t in Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude)) t.SetTroops(6);
        var card = Give(_alice, CardType.ScatterBombLand);
        Assert.IsTrue(Play(card));
        var hit = _cr.Abilities.LastScatterTargets;
        Assert.AreEqual(3, hit.Count);
        foreach (var t in hit)
            Assert.AreEqual(t.Owner == _alice ? 6 : 3, t.Troops, t.Name);
        yield break;
    }

    [UnityTest]
    public IEnumerator TheMother_DestroysOneUnitInEveryTerritoryOfTheContinentRolled()
    {
        var all = Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude);
        foreach (var t in all) t.SetTroops(4);
        var card = Give(_alice, CardType.TheMother);
        Assert.IsTrue(Play(card));
        var continent = _cr.Abilities.LastContinentHit;
        Assert.IsNotNull(continent);
        foreach (var t in all)
            Assert.AreEqual(continent.Territories.Contains(t) ? 3 : 4, t.Troops, t.Name);
        yield break;
    }

    [UnityTest]
    public IEnumerator AssassinBomb_KillsTheCommander_On3OrMore()
    {
        var card = Give(_alice, CardType.AssassinBomb);
        var t = _bob.Territories.First(x => x.CommandersList.Any(c => c.CommanderType == CommanderType.Diplomat));
        Assert.IsFalse(Play(card, t, CommanderType.NuclearCommander), "Bob has no Nuclear commander there");
        Assert.IsTrue(Play(card, t, CommanderType.Diplomat));
        var roll = _cr.Abilities.LastRoll;
        Assert.That(roll, Is.InRange(1, 8));
        Assert.AreEqual(roll < 3, _bob.HasCommanderInPlay(CommanderType.Diplomat));
        yield break;
    }

    [UnityTest]
    public IEnumerator NotImplementedCards_CannotBePlayed()
    {
        foreach (var type in new[] { CardType.DecoyRevealed, CardType.Armageddon })
        {
            var card = Give(_alice, type);
            Assert.AreEqual(CardTiming.NotImplemented, card.Timing, type.ToString());
            Assert.IsFalse(Play(card));
        }
        yield break;
    }

    // ---------- Scoring ----------

    [UnityTest]
    public IEnumerator Score_TerritoriesPlusContinentsPlusColonyInfluence_IfTheCommanderLives()
    {
        var baseScore = _alice.GetTerritoryCount()
                        + ContinentRepository.Instance.Continents.Values.Where(c => c.IsOwnedByPlayer(_alice)).Sum(c => c.BonusTroops);
        Assert.AreEqual(baseScore, Scoring.ScoreOf(_alice));

        Give(_alice, CardType.ColonyInfluenceD);
        Assert.AreEqual(baseScore + 3, Scoring.ScoreOf(_alice), "Diplomat alive: +3");

        foreach (var t in _alice.Territories)
            foreach (var c in t.CommandersList.Where(c => c.CommanderType == CommanderType.Diplomat).ToList())
                t.RemoveCommander(c);
        Assert.AreEqual(baseScore, Scoring.ScoreOf(_alice), "Diplomat dead: no points");
        yield break;
    }

    [UnityTest]
    public IEnumerator Ranking_HighestScoreFirst_TiesByEnergy()
    {
        var ranking = Scoring.Ranking(_gm.Players);
        Assert.AreEqual(Scoring.ScoreOf(ranking[0]) >= Scoring.ScoreOf(ranking[1]), true);

        // Force a tie: give Bob Alice's surplus territories until scores match, then energy decides.
        while (Scoring.ScoreOf(_alice) > Scoring.ScoreOf(_bob))
            _alice.Territories.First(t => t.CommandersList.Count == 0 && !t.containsSpaceStation).SetOwner(_bob, 1);
        while (Scoring.ScoreOf(_bob) > Scoring.ScoreOf(_alice))
            _bob.Territories.First(t => t.CommandersList.Count == 0 && !t.containsSpaceStation).SetOwner(_alice, 1);
        if (Scoring.ScoreOf(_alice) == Scoring.ScoreOf(_bob))
        {
            _alice.SetEnergy(1);
            _bob.SetEnergy(5);
            Assert.AreEqual(_bob, Scoring.Ranking(_gm.Players)[0], "Tie: more energy wins");
        }
        yield break;
    }

    // ---------- Helpers ----------

    private Card Give(Player p, CardType type)
    {
        var card = _cr.AllCards.First(c => c.CardType == type && _cr.CardsInDeck.Contains(c));
        _cr.CardsInDeck.Remove(card);
        p.AddCard(card);
        return card;
    }

    private bool Play(Card card, params object[] targets) =>
        _gm.HandlePlayerAction(new PlayCommandCardAction(_alice, _gm.Turn, card, targets));

    private void EndPhase() => _gm.HandlePlayerAction(new EndPhaseAction(_gm.CurrentPlayer, _gm.Turn));

    private (Territory from, Territory to) FortifyPair()
    {
        foreach (var from in _alice.Territories.Where(t => t.Troops > 1))
        {
            var to = from.GetFriendlyNeighbours().FirstOrDefault();
            if (to != null) return (from, to);
        }
        Assert.Fail("No fortify move available");
        return (null, null);
    }
}
