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
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// RW-2..5: the defender's reaction window and the defense cards.
// The window opens before EVERY roll, only for the DEFENDER, only if they hold a defense card they can
// play now (commander in play, enough energy, not jammed). Several cards per invasion are allowed.
public class ReactionWindowTests
{
    private const string ConfigPath = "Assets/ScriptableObjects/StartingPlayersConfiguration.asset";
    private const string HumanConfigPath = "Assets/ScriptableObjects/PlayerConfig/Human_Player_conf.asset";

    private StartingPlayersConfiguration _config;
    private List<PlayerCreationConfiguration> _savedConfig;
    private GameManager _gm;
    private CardRepository _cr;
    private Player _alice, _bob;
    private Territory _origin, _target;

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

        // Alice's Attack phase; a known front: Alice's origin (10 units) next to Bob's target (2 units, no station/commanders).
        for (int i = 0; i < 10 && _gm.IsBidding; i++)
            Assert.IsTrue(_gm.SubmitBid(_gm.CurrentPlayer == _alice ? 1 : 0));
        TestGame.ChooseEarliest(_gm);
        Assert.AreEqual(_alice, _gm.CurrentPlayer);
        var rp = (ReinforcePhase)_gm.CurrentPhase;
        _gm.HandlePlayerAction(new ReinforceAction(_alice, _gm.Turn, _alice.Territories.First(), rp.RemainingTroopsToPlace));
        if (_gm.CurrentPhase is ReinforcePhase) _gm.HandlePlayerAction(new EndPhaseAction(_alice, _gm.Turn));
        Assert.IsInstanceOf<AttackPhase>(_gm.CurrentPhase);

        _origin = _alice.Territories.First(t => t.GetEnemyNeighbours().Any(n => !n.containsSpaceStation && n.CommandersList.Count == 0));
        _target = _origin.GetEnemyNeighbours().First(n => !n.containsSpaceStation && n.CommandersList.Count == 0);
        _origin.SetTroops(10);
        _target.SetTroops(2);
        _bob.SetEnergy(10);
        Assert.IsTrue(_bob.HasCommanderInPlay(CommanderType.Diplomat) && _bob.HasCommanderInPlay(CommanderType.LandCommander),
            "Bob starts with a Diplomat and a Land Commander");
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

    // ---------- When the window opens ----------

    [UnityTest]
    public IEnumerator NoDefenseCard_NoWindow_DiceRollStraightAway()
    {
        Assert.IsTrue(Attack());
        Assert.IsNull(_gm.PendingDecision);
        Assert.AreEqual(1, _gm.AttackPhase.AttackResults.Count());
        yield break;
    }

    [UnityTest]
    public IEnumerator DefenderWithAPlayableCard_IsAskedBeforeTheRoll_AndOnlyTheDefender()
    {
        var card = Give(_bob, CardType.StealthModsL);
        Assert.IsTrue(Attack());
        var d = _gm.PendingDecision;
        Assert.IsNotNull(d);
        Assert.AreEqual(_gm.GetPlayerId(_bob), d.PlayerId);
        Assert.AreEqual(DecisionKind.DefenseReaction, d.Kind);
        CollectionAssert.Contains(d.Options, CommandCards.OptionFor(card));
        CollectionAssert.Contains(d.Options, DecisionOptions.Pass);
        CollectionAssert.Contains(d.Options, DecisionOptions.PassForRestOfTurn);
        Assert.AreEqual(0, _gm.AttackPhase.AttackResults.Count(), "No roll yet");
        yield break;
    }

    [UnityTest]
    public IEnumerator NoWindow_WithoutTheCommander_WhenJammed_OrWithoutEnergy()
    {
        Give(_bob, CardType.LandDeathTrap); // cost 3, needs the Land Commander
        _target.SetTroops(40); // several rolls without a conquest

        _bob.SetEnergy(2);
        Assert.IsTrue(Attack());
        Assert.IsNull(_gm.PendingDecision, "Not enough energy");

        _bob.SetEnergy(10);
        _bob.canPlayCommandCards = false;
        Assert.IsTrue(Attack());
        Assert.IsNull(_gm.PendingDecision, "Frequency Jam");

        _bob.canPlayCommandCards = true;
        foreach (var t in _bob.Territories)
            foreach (var c in t.CommandersList.Where(c => c.CommanderType == CommanderType.LandCommander).ToList())
                t.RemoveCommander(c);
        Assert.IsTrue(Attack());
        Assert.IsNull(_gm.PendingDecision, "Land Commander is dead");
        yield break;
    }

    [UnityTest]
    public IEnumerator Window_OpensBeforeEveryRoll_UntilPassForRestOfTurn()
    {
        Give(_bob, CardType.StealthModsL);
        Assert.IsTrue(Attack());
        Assert.IsTrue(Answer(DecisionOptions.Pass));
        Assert.AreEqual(1, _gm.AttackPhase.AttackResults.Count());

        if (_target.Owner == _bob)
        {
            Assert.IsTrue(Attack());
            Assert.IsNotNull(_gm.PendingDecision, "Asked again on the next roll");
            Assert.IsTrue(Answer(DecisionOptions.PassForRestOfTurn));
            Assert.AreEqual(2, _gm.AttackPhase.AttackResults.Count());
        }
        if (_target.Owner == _bob)
        {
            Assert.IsTrue(Attack());
            Assert.IsNull(_gm.PendingDecision, "Passed for the rest of the turn");
        }
        yield break;
    }

    // ---------- Defense cards ----------

    [UnityTest]
    public IEnumerator StealthMods_AddsThreeDefenders_ThenTheDiceRoll()
    {
        var card = Give(_bob, CardType.StealthModsL);
        Assert.IsTrue(Attack());
        Assert.IsTrue(Answer(CommandCards.OptionFor(card)));
        Assert.IsNull(_gm.PendingDecision, "No more cards: roll");
        var r = _gm.AttackPhase.LastAttackResult;
        Assert.AreEqual(2 + 3 - r.DefenderLosses, _target.Troops, "2 + 3 stealth MODs - losses");
        Assert.IsFalse(_bob.Cards.Contains(card));
        yield break;
    }

    [UnityTest]
    public IEnumerator SeveralCards_OnTheSameInvasion()
    {
        var a = Give(_bob, CardType.StealthModsL);
        var b = Give(_bob, CardType.StealthModsL);
        Assert.IsTrue(Attack());
        Assert.IsTrue(Answer(CommandCards.OptionFor(a)));
        Assert.IsNotNull(_gm.PendingDecision, "Asked again: Bob still has a card");
        CollectionAssert.Contains(_gm.PendingDecision.Options, CommandCards.OptionFor(b));
        Assert.IsTrue(Answer(CommandCards.OptionFor(b)));
        Assert.IsNull(_gm.PendingDecision);
        Assert.AreEqual(2 + 6 - _gm.AttackPhase.LastAttackResult.DefenderLosses, _target.Troops);
        yield break;
    }

    [UnityTest]
    public IEnumerator LandDeathTrap_DestroysHalfTheAttackers_CostsThree()
    {
        var card = Give(_bob, CardType.LandDeathTrap);
        Assert.IsTrue(Attack());
        Assert.IsTrue(Answer(CommandCards.OptionFor(card)));
        Assert.AreEqual(7, _bob.GetEnergy(), "10 - 3");
        var r = _gm.AttackPhase.LastAttackResult;
        Assert.AreEqual(5 - r.AttackerLosses, _origin.Troops, "10 units -> 5, then the roll");
        yield break;
    }

    [UnityTest]
    public IEnumerator LandDeathTrap_CanStopAnInvasion_WhenNoAttackerIsLeftToRoll()
    {
        _origin.SetTroops(2); // 1 can attack
        var card = Give(_bob, CardType.LandDeathTrap);
        Assert.IsTrue(Attack(1));
        Assert.IsTrue(Answer(CommandCards.OptionFor(card)));
        Assert.AreEqual(1, _origin.Troops);
        Assert.AreEqual(0, _gm.AttackPhase.AttackResults.Count(), "Nobody left to attack: no roll");
        Assert.AreEqual(AttackState.Attacking, _gm.AttackPhase.State);
        yield break;
    }

    [UnityTest]
    public IEnumerator StealthStation_PutsAStationInTheDefendingTerritory_BeforeTheRoll()
    {
        var card = Give(_bob, CardType.StealthStation);
        Assert.IsTrue(Attack());
        Assert.IsTrue(Answer(CommandCards.OptionFor(card)));
        Assert.IsTrue(_target.containsSpaceStation || _target.Owner == _alice, "Station placed (captured if Alice won)");
        Assert.AreEqual(2, _gm.AttackPhase.LastAttackResult.DefenderRolls.Length);
        yield break;
    }

    [UnityTest]
    public IEnumerator CeaseFire_StopsTheInvasion_AndNoMoreAttacksOnThatPlayerThisTurn()
    {
        var card = Give(_bob, CardType.CeaseFire);
        Assert.IsTrue(Attack());
        Assert.IsTrue(Answer(CommandCards.OptionFor(card)));
        Assert.AreEqual(8, _bob.GetEnergy(), "10 - 2");
        Assert.AreEqual(0, _gm.AttackPhase.AttackResults.Count(), "No roll");
        Assert.AreEqual(2, _target.Troops);
        Assert.IsTrue(_gm.AttackPhase.IsCeaseFireAgainst(_bob));
        Assert.IsFalse(Attack(), "Alice can't attack Bob again this turn");
        yield break;
    }

    [UnityTest]
    public IEnumerator Evacuation_MovesUnitsAndCommandersOut_AndTheAttackerTakesTheTerritoryWithoutDice()
    {
        _target.SetTroops(4);
        _target.AddCommander(new Commander(CommanderType.NuclearCommander));
        var destination = CommandCards.EvacuationDestination(_bob, _target);
        var before = destination.Troops;
        var card = Give(_bob, CardType.Evacuation);

        Assert.IsTrue(Attack());
        Assert.IsTrue(Answer(CommandCards.OptionFor(card)));

        Assert.AreEqual(before + 4, destination.Troops, "All 4 units evacuated");
        Assert.IsTrue(destination.CommandersList.Any(c => c.CommanderType == CommanderType.NuclearCommander), "Commander evacuated too");
        Assert.IsTrue(_bob.HasCommanderInPlay(CommanderType.NuclearCommander));
        Assert.AreEqual(_alice, _target.Owner, "Attacker occupies the empty territory");
        var r = _gm.AttackPhase.LastAttackResult;
        Assert.AreEqual(0, r.AttackerRolls.Length, "No dice");
        Assert.AreEqual(0, r.AttackerLosses);
        Assert.AreEqual(AttackState.Fortifying, _gm.AttackPhase.State, "Alice now moves units in");
        yield break;
    }

    // ---------- AI defender ----------

    [UnityTest]
    public IEnumerator AIDefender_PlaysFreeStealthMods()
    {
        var mods = Give(_bob, CardType.StealthModsL);
        Assert.IsTrue(Attack());
        Assert.AreEqual(CommandCards.OptionFor(mods), DefenderAI.Choose(_gm, _gm.PendingDecision));
        yield break;
    }

    [UnityTest]
    public IEnumerator AIDefender_KeepsTheDeathTrapForABigAttack()
    {
        var trap = Give(_bob, CardType.LandDeathTrap);
        _origin.SetTroops(3);
        Assert.IsTrue(Attack(2));
        Assert.AreEqual(DecisionOptions.Pass, DefenderAI.Choose(_gm, _gm.PendingDecision), "3 attackers: not worth 3 energy");
        Assert.IsTrue(Answer(DecisionOptions.Pass));

        if (_target.Owner == _bob)
        {
            _origin.SetTroops(10);
            Assert.IsTrue(Attack());
            Assert.AreEqual(CommandCards.OptionFor(trap), DefenderAI.Choose(_gm, _gm.PendingDecision), "10 attackers: trap them");
        }
        yield break;
    }

    // ---------- Helpers ----------

    private bool Attack(int troops = 3) =>
        _gm.HandlePlayerAction(new AttackAction(_alice, _gm.Turn, _gm.AttackPhase.AttackTurn, _origin, _target,
            Mathf.Min(troops, _origin.GetAvailableTroops())));

    private bool Answer(string option) =>
        _gm.HandlePlayerAction(new DecisionResponseAction(_bob, _gm.Turn, option));

    private Card Give(Player p, CardType type)
    {
        var card = _cr.AllCards.First(c => c.CardType == type && _cr.CardsInDeck.Contains(c));
        _cr.CardsInDeck.Remove(card);
        p.AddCard(card);
        return card;
    }
}
