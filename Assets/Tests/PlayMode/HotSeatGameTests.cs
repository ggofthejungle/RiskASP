using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Actions;
using Map;
using NUnit.Framework;
using player;
using TurnPhases;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Human vs human (hot-seat) rules for a 2-player game. Each test loads the Main scene fresh.
//
// Expected behaviour:
//  - Every year starts with bidding: each alive player bids 0..their current energy.
//  - Invalid bids (negative, or more than the player has) are rejected and change nothing.
//  - Highest bid plays first; the bid is subtracted from the player's energy; energy never goes negative.
//  - Each turn starts with energy income (3, or territories / 3 with 12+ territories).
//  - During a year every alive player takes exactly ONE turn (Reinforce -> Attack -> Fortify).
//  - After the last player's turn the year increases and bidding opens again.
//  - After year 5 the game is over.
//  - No errors are logged at any point (the test runner fails on Debug.LogError).
public class HotSeatGameTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        _config = AssetDatabase.LoadAssetAtPath<StartingPlayersConfiguration>(ConfigPath);
        var human = AssetDatabase.LoadAssetAtPath<PlayerConfiguration>(HumanConfigPath);
        var red = AssetDatabase.LoadAssetAtPath<PlayerColor>("Assets/ScriptableObjects/PlayerColors/Red.asset");
        var blue = AssetDatabase.LoadAssetAtPath<PlayerColor>("Assets/ScriptableObjects/PlayerColors/Blue.asset");
        Assert.IsNotNull(_config, "StartingPlayersConfiguration asset not found");
        Assert.IsNotNull(human, "Human player config not found");

        _savedConfig = new List<PlayerCreationConfiguration>(_config.PlayersConfiguration);
        _config.PlayersConfiguration.Clear();
        _config.PlayersConfiguration.Add(new PlayerCreationConfiguration { PlayerConfiguration = human, PlayerName = "Alice", PlayerColor = red });
        _config.PlayersConfiguration.Add(new PlayerCreationConfiguration { PlayerConfiguration = human, PlayerName = "Bob", PlayerColor = blue });
#endif
        yield return SceneManager.LoadSceneAsync("Main"); // wait until the old scene (and its GameManager) is gone
        for (int i = 0; i < 300; i++)
        {
            _gm = GameManager.Instance;
            if (_gm != null && _gm.IsBidding) break;
            yield return null;
        }
        Assert.IsNotNull(_gm, "GameManager did not load");
        Assert.IsTrue(_gm.IsBidding, "Game should start in bidding");
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

    // ---------- Bidding ----------

    [UnityTest]
    public IEnumerator Game_StartsInYear1_WithBidding_And2HumanPlayers()
    {
        Assert.AreEqual(1, _gm.Year);
        Assert.AreEqual(2, _gm.Players.Count(p => !p.IsDead()));
        Assert.IsFalse(_gm.Players.Any(p => p.IsBot()), "Both players should be human");
        Assert.IsTrue(_gm.Players.All(p => p.GetEnergy() == 3), "Everyone starts with 3 energy");
        yield break;
    }

    [UnityTest]
    public IEnumerator Bid_MoreThanEnergy_IsRejected()
    {
        var bidder = _gm.CurrentPlayer;
        var energy = bidder.GetEnergy();
        Assert.IsFalse(_gm.SubmitBid(energy + 1));
        Assert.AreEqual(bidder, _gm.CurrentPlayer, "Same player must still be bidding");
        Assert.AreEqual(-1, bidder._energyBid, "Rejected bid must not be stored");
        Assert.AreEqual(energy, bidder.GetEnergy(), "Energy unchanged");
        Assert.IsTrue(_gm.IsBidding);
        yield break;
    }

    [UnityTest]
    public IEnumerator Bid_Negative_IsRejected()
    {
        var bidder = _gm.CurrentPlayer;
        Assert.IsFalse(_gm.SubmitBid(-1));
        Assert.AreEqual(bidder, _gm.CurrentPlayer);
        Assert.IsTrue(_gm.IsBidding);
        yield break;
    }

    [UnityTest]
    public IEnumerator HighestBid_GoesFirst_AndBidIsPaid()
    {
        var first = _gm.CurrentPlayer;
        Assert.IsTrue(_gm.SubmitBid(2));
        var second = _gm.CurrentPlayer;
        Assert.AreNotEqual(first, second, "Bidding moves to the other player");
        Assert.IsTrue(_gm.SubmitBid(0));

        Assert.IsFalse(_gm.IsBidding, "Bidding is over after both bid");
        Assert.IsTrue(_gm.IsChoosingOrder, "Then the highest bidder chooses a turn position");
        Assert.AreEqual(first, _gm.CurrentPlayer, "Highest bidder chooses first");
        Assert.IsTrue(_gm.ChooseTurnPosition(1));
        Assert.IsFalse(_gm.IsChoosingOrder, "The last player gets the remaining position automatically");
        Assert.AreEqual(first, _gm.CurrentPlayer, "Highest bidder plays first (chose 1st)");
        Assert.IsInstanceOf<ReinforcePhase>(_gm.CurrentPhase);
        // The bid is paid when bidding ends; then the first player's turn starts and adds that turn's income.
        Assert.AreEqual(3 - 2 + TurnIncome(first), first.GetEnergy(), "3 - 2 + income");
        Assert.AreEqual(3, second.GetEnergy(), "3 - 0 = 3 (their turn hasn't started yet)");
        Assert.AreEqual(GameState.Playing, _gm.GameState);
        yield break;
    }

    [UnityTest]
    public IEnumerator LowBidder_CanStillGoFirst_WhenTheyBidMore()
    {
        var first = _gm.CurrentPlayer;
        Assert.IsTrue(_gm.SubmitBid(0));
        var second = _gm.CurrentPlayer;
        Assert.IsTrue(_gm.SubmitBid(3));
        TestGame.ChooseEarliest(_gm);
        Assert.AreEqual(second, _gm.CurrentPlayer, "The player who bid 3 plays first");
        Assert.AreEqual(0 + TurnIncome(second), second.GetEnergy(), "3 - 3 + income");
        yield break;
    }

    // ---------- Choosing turn order (2210: highest bidder picks their position) ----------

    [UnityTest]
    public IEnumerator HighestBidder_CanChooseToPlaySecond()
    {
        var first = _gm.CurrentPlayer;
        Assert.IsTrue(_gm.SubmitBid(2));
        var second = _gm.CurrentPlayer;
        Assert.IsTrue(_gm.SubmitBid(0));

        Assert.AreEqual(first, _gm.CurrentPlayer, "Highest bidder chooses");
        Assert.IsTrue(_gm.ChooseTurnPosition(2));
        Assert.IsFalse(_gm.IsChoosingOrder);
        Assert.AreEqual(second, _gm.CurrentPlayer, "The other player plays 1st");
        Assert.IsInstanceOf<ReinforcePhase>(_gm.CurrentPhase);

        PlaySimpleTurn();
        Assert.AreEqual(first, _gm.CurrentPlayer, "Highest bidder plays 2nd, as chosen");
        Assert.AreEqual(1, _gm.Year);
        yield break;
    }

    [UnityTest]
    public IEnumerator ChoosingAPositionThatDoesNotExist_IsRejected()
    {
        Assert.IsTrue(_gm.SubmitBid(1));
        Assert.IsTrue(_gm.SubmitBid(0));
        var chooser = _gm.CurrentPlayer;
        Assert.IsFalse(_gm.ChooseTurnPosition(0));
        Assert.IsFalse(_gm.ChooseTurnPosition(3));
        Assert.IsTrue(_gm.IsChoosingOrder, "Still choosing");
        Assert.AreEqual(chooser, _gm.CurrentPlayer);
        CollectionAssert.AreEqual(new[] { 1, 2 }, _gm.AvailableTurnPositions);
        yield break;
    }

    [UnityTest]
    public IEnumerator ChoosingOutsideTheChoosingStep_IsIgnored()
    {
        Assert.IsTrue(_gm.IsBidding);
        Assert.IsFalse(_gm.ChooseTurnPosition(1), "Not during bidding");
        BidAll(0);
        Assert.IsFalse(_gm.ChooseTurnPosition(1), "Not during a turn");
        yield break;
    }

    // ---------- Turns and years ----------

    [UnityTest]
    public IEnumerator Year1_EachPlayerGetsExactlyOneTurn_ThenYear2BiddingOpens()
    {
        BidAll(0);
        var turnsTaken = PlayRestOfYear();
        yield return null;

        Assert.AreEqual(2, turnsTaken.Count, "Two turns in year 1");
        CollectionAssert.AreEquivalent(_gm.Players, turnsTaken, "Each player played exactly once");
        Assert.AreEqual(2, _gm.Year);
        Assert.IsTrue(_gm.IsBidding, "Year 2 starts with bidding");
    }

    [UnityTest]
    public IEnumerator FullGame_5Years_EveryoneGetsOneTurnPerYear_ThenGameOver()
    {
        for (int year = 1; year <= GameManager.MaxYears; year++)
        {
            Assert.AreEqual(year, _gm.Year);
            Assert.IsTrue(_gm.IsBidding, $"Year {year} should start with bidding");
            BidAll(1);
            Assert.IsTrue(_gm.Players.All(p => p.GetEnergy() >= 0), "Energy never negative");

            var alive = _gm.Players.Count(p => !p.IsDead());
            var turns = PlayRestOfYear();
            if (_gm.GameState == GameState.Over) break;
            Assert.AreEqual(alive, turns.Count, $"Year {year}: one turn per player");
            Assert.AreEqual(alive, turns.Distinct().Count(), $"Year {year}: nobody played twice");
            yield return null;
        }

        Assert.AreEqual(GameState.Over, _gm.GameState, "Game ends after year 5");
    }

    // ---------- Helpers ----------

    // Energy gained at the start of a player's turn (ReinforcePhase): 3, or territories / 3 with 12+.
    private static int TurnIncome(Player p)
    {
        var territories = p.GetTerritoryCount();
        return territories < 12 ? 3 : territories / 3;
    }

    // Every bidder bids `bid`, or all their energy if they have less.
    private void BidAll(int bid)
    {
        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(Mathf.Min(bid, _gm.CurrentPlayer.GetEnergy())));
        Assert.IsFalse(_gm.IsBidding);
        TestGame.ChooseEarliest(_gm);
    }

    // Plays turns until bidding opens again (or the game ends). Returns who played, in order.
    private List<Player> PlayRestOfYear()
    {
        var played = new List<Player>();
        for (int guard = 0; guard < 20 && !_gm.IsBidding && _gm.GameState != GameState.Over; guard++)
        {
            played.Add(_gm.CurrentPlayer);
            PlaySimpleTurn();
        }
        return played;
    }

    // Reinforce the strongest border territory, attack its weakest enemy neighbour a few times, end turn.
    private void PlaySimpleTurn()
    {
        var p = _gm.CurrentPlayer;
        var territories = Object.FindObjectsByType<Territory>(FindObjectsInactive.Exclude);
        var border = territories.Where(t => t.Owner == p && t.HasAnyNeighbourEnemy())
                                .OrderByDescending(t => t.Troops).FirstOrDefault();

        Assert.IsInstanceOf<ReinforcePhase>(_gm.CurrentPhase, $"{p.Name}'s turn should start in Reinforce");
        var rp = (ReinforcePhase)_gm.CurrentPhase;
        if (border != null && rp.RemainingTroopsToPlace > 0)
            _gm.HandlePlayerAction(new ReinforceAction(p, _gm.Turn, border, rp.RemainingTroopsToPlace));
        if (_gm.CurrentPhase is ReinforcePhase)
            _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn));

        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);
        var ap = (AttackPhase)_gm.CurrentPhase;
        if (border != null)
        {
            var target = border.GetEnemyNeighbours().OrderBy(t => t.Troops).FirstOrDefault();
            for (int i = 0; i < 3 && target != null && target.Owner != p && border.Troops > 1; i++)
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
        _gm.HandlePlayerAction(new EndPhaseAction(p, _gm.Turn)); // end Fortify -> next player / next year
    }
}
