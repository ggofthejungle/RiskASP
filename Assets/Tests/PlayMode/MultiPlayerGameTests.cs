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

// 4- and 5-player hot-seat games, start to finish.
public class MultiPlayerGameTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";
    private static readonly string[] Colors = { "Red", "Blue", "Green", "Yellow", "Purple", "Orange" };

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;

    [TearDown]
    public void TearDown()
    {
        if (_config != null && _savedConfig != null)
        {
            _config.PlayersConfiguration.Clear();
            _config.PlayersConfiguration.AddRange(_savedConfig);
        }
    }

    [UnityTest] public IEnumerator FourPlayers_FullGame() => FullGame(4);
    [UnityTest] public IEnumerator FivePlayers_FullGame() => FullGame(5);
    [UnityTest] public IEnumerator FourPlayers_OnlyTheAttackedPlayerIsAskedToReact() => OnlyDefenderIsAsked(4);
    [UnityTest] public IEnumerator FivePlayers_OnlyTheAttackedPlayerIsAskedToReact() => OnlyDefenderIsAsked(5);

    private IEnumerator FullGame(int n)
    {
        yield return LoadGame(n);

        // Setup
        Assert.AreEqual(n, _gm.Players.Count);
        Assert.IsFalse(_gm.Players.Any(p => p.IsBot()));
        var territories = Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude);
        Assert.AreEqual(42, _gm.Players.Sum(p => p.GetTerritoryCount()), "Every territory is owned");
        Assert.IsTrue(_gm.Players.All(p => p.GetTerritoryCount() >= 42 / n), "Territories dealt evenly");
        foreach (var p in _gm.Players)
        {
            Assert.IsTrue(p.HasCommanderInPlay(CommanderType.Diplomat) && p.HasCommanderInPlay(CommanderType.LandCommander), p.Name);
            Assert.AreEqual(1, Purchases.StationsOf(p), p.Name);
            Assert.AreEqual(3, p.GetEnergy(), p.Name);
        }
        Assert.AreEqual(n, territories.Count(t => t.containsSpaceStation));

        for (int year = 1; year <= GameManager.MaxYears && _gm.GameState != GameState.Over; year++)
        {
            Assert.AreEqual(year, _gm.Year);
            Assert.IsTrue(_gm.IsBidding, $"Year {year} starts with bidding");

            // Everyone bids something different where possible.
            var bids = new Dictionary<Player, int>();
            for (int i = 0; i < 20 && _gm.IsBidding; i++)
            {
                var p = _gm.CurrentPlayer;
                var bid = Mathf.Min(p.GetEnergy(), (bids.Count + year) % 4);
                bids[p] = bid;
                Assert.IsTrue(_gm.SubmitBid(bid));
            }
            var alive = _gm.Players.Count(p => !p.IsDead());
            Assert.AreEqual(alive, bids.Count, "Every alive player bid once");
            Assert.IsTrue(_gm.IsChoosingOrder);

            // Choosers come in bid order; each takes the LAST free position, so play order is reversed.
            var choosers = new List<Player>();
            for (int i = 0; i < 20 && _gm.IsChoosingOrder; i++)
            {
                choosers.Add(_gm.CurrentPlayer);
                Assert.IsTrue(_gm.ChooseTurnPosition(_gm.AvailableTurnPositions.Last()));
            }
            Assert.AreEqual(alive - 1, choosers.Count, "The last player is placed automatically");
            for (int i = 1; i < choosers.Count; i++)
                Assert.GreaterOrEqual(bids[choosers[i - 1]], bids[choosers[i]], "Choosers in bid order");

            var played = new List<Player>();
            for (int guard = 0; guard < 20 && !_gm.IsBidding && _gm.GameState != GameState.Over; guard++)
            {
                played.Add(_gm.CurrentPlayer);
                PlaySimpleTurn();
            }
            if (_gm.GameState == GameState.Over) break;
            Assert.AreEqual(alive, played.Count, $"Year {year}: one turn each");
            Assert.AreEqual(alive, played.Distinct().Count(), $"Year {year}: nobody twice");
            if (_gm.Players.All(p => !p.IsDead()))
                CollectionAssert.AreEqual(Enumerable.Reverse(choosers).ToList(), played.Skip(1).ToList(),
                    "Play order is the reverse of the choosing order (the auto-placed player goes 1st)");
            Assert.AreEqual(choosers[0], played.Last(), "Whoever chose first took the last position");
            Assert.IsTrue(_gm.Players.All(p => p.GetEnergy() >= 0), "Energy never negative");
            yield return null;
        }

        Assert.AreEqual(GameState.Over, _gm.GameState);
        Assert.AreEqual(n, _gm.FinalRanking.Count);
        var best = _gm.Players.Where(p => !p.IsDead()).Max(Scoring.ScoreOf);
        Assert.AreEqual(best, Scoring.ScoreOf(_gm.Winner), "Winner has the top score");
    }

    private IEnumerator OnlyDefenderIsAsked(int n)
    {
        yield return LoadGame(n);
        for (int i = 0; i < 20 && _gm.IsBidding; i++) Assert.IsTrue(_gm.SubmitBid(0));
        TestGame.ChooseEarliest(_gm);
        var attacker = _gm.CurrentPlayer;
        PlaceAllAndEndReinforce();

        var origin = attacker.Territories.First(t => t.HasAnyNeighbourEnemy());
        var target = origin.GetEnemyNeighbours().First();
        var defender = target.Owner;
        origin.SetTroops(10);
        foreach (var p in _gm.Players.Where(p => p != attacker))
            Give(p, CardType.StealthModsL); // everyone could react, but only the defender is asked

        Assert.IsTrue(_gm.HandlePlayerAction(new AttackAction(attacker, _gm.Turn, _gm.AttackPhase.AttackTurn, origin, target, 3)));
        Assert.IsNotNull(_gm.PendingDecision);
        Assert.AreEqual(_gm.GetPlayerId(defender), _gm.PendingDecision.PlayerId);
        Assert.AreEqual(defender, _gm.InputPlayer);
        Assert.AreEqual(attacker, _gm.CurrentPlayer);
    }

    // ---------- Helpers ----------

    private IEnumerator LoadGame(int n)
    {
#if UNITY_EDITOR
        _config = AssetDatabase.LoadAssetAtPath<StartingPlayersConfiguration>(ConfigPath);
        var human = AssetDatabase.LoadAssetAtPath<PlayerConfiguration>(HumanConfigPath);
        _savedConfig = new List<PlayerCreationConfiguration>(_config.PlayersConfiguration);
        _config.PlayersConfiguration.Clear();
        for (int i = 0; i < n; i++)
        {
            var color = AssetDatabase.LoadAssetAtPath<PlayerColor>($"Assets/ScriptableObjects/PlayerColors/{Colors[i]}.asset");
            Assert.IsNotNull(color, Colors[i]);
            _config.PlayersConfiguration.Add(new PlayerCreationConfiguration
                { PlayerConfiguration = human, PlayerName = $"P{i + 1}", PlayerColor = color });
        }
#endif
        yield return SceneManager.LoadSceneAsync("Main");
        for (int i = 0; i < 300; i++)
        {
            _gm = GameManager.Instance;
            if (_gm != null && _gm.IsBidding) break;
            yield return null;
        }
        Assert.IsNotNull(_gm);
        Assert.IsTrue(_gm.IsBidding);
    }

    private void Give(Player p, CardType type)
    {
        var cr = CardRepository.Instance;
        var card = cr.AllCards.First(c => c.CardType == type && cr.CardsInDeck.Contains(c));
        cr.CardsInDeck.Remove(card);
        p.AddCard(card);
    }

    private void PlaceAllAndEndReinforce()
    {
        var p = _gm.CurrentPlayer;
        if (_gm.CurrentPhase is ReinforcePhase rp)
        {
            var border = p.Territories.Where(t => t.HasAnyNeighbourEnemy()).OrderByDescending(t => t.Troops).FirstOrDefault()
                         ?? p.Territories.First();
            if (rp.RemainingTroopsToPlace > 0)
                _gm.HandlePlayerAction(new ReinforceAction(p, _gm.Turn, border, rp.RemainingTroopsToPlace));
            if (_gm.CurrentPhase is ReinforcePhase)
                _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn));
        }
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);
    }

    private void PlaySimpleTurn()
    {
        var p = _gm.CurrentPlayer;
        PlaceAllAndEndReinforce();
        var ap = _gm.AttackPhase;
        var border = p.Territories.Where(t => t.HasAnyNeighbourEnemy()).OrderByDescending(t => t.Troops).FirstOrDefault();
        if (border != null)
        {
            var target = border.GetEnemyNeighbours().OrderBy(t => t.Troops).First();
            for (int i = 0; i < 3 && target.Owner != p && border.Troops > 1 && ap.State == AttackState.Attacking; i++)
            {
                var troops = Mathf.Min(3, border.Troops - 1);
                var attack = new AttackAction(p, _gm.Turn, ap.AttackTurn, border, target, troops);
                _gm.HandlePlayerAction(attack);
                if (ap.State == AttackState.Fortifying)
                    _gm.HandlePlayerAction(new AttackReinforceAction(p, _gm.Turn, ap.AttackTurn, attack, troops));
            }
        }
        _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn)); // end Attack
        Assert.IsInstanceOf<FortifyPhase>(_gm.CurrentPhase);
        _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn)); // end Fortify
    }
}
