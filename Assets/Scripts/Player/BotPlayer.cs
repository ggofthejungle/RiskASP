using System.Collections.Generic;
using System.Linq;
using TurnPhases;
using UnityEngine;

namespace player
{
    [RequireComponent(typeof(Player))]
    public class BotPlayer : MonoBehaviour
    {
        public BotConfiguration BotConfiguration => _botConfiguration;
        [SerializeField] private BotConfiguration _botConfiguration;

        // The seat's pick from the menu; the prefab's own one is only the default.
        public void SetConfiguration(BotConfiguration configuration)
        {
            if (configuration != null) _botConfiguration = configuration;
        }

        public BotBrain BotBrain => _botBrain;
        [SerializeField] private BotBrain _botBrain;

        public Player Player => _player;
        private Player _player;
        private GameManager _gm;


        private void Awake()
        {
            _player = GetComponent<Player>();
            if (_botBrain == null)
            {
                Debug.Log("BotPlayer Awake: BotBrain is null, finding a BotBrain in the scene");
                _botBrain = FindAnyObjectByType<BotBrain>();
            }
        }

        private void OnEnable()
        {
            _gm = GameManager.Instance;
            if (_gm == null) return; // the match scene is being torn down
            _gm.OnTurnPhaseChanged += OnTurnPhaseChanged;
            _gm.AttackPhase.OnAttackTurn += OnAttackPhaseTurn;
            _gm.FortifyPhase.OnFortifyAction += OnFortified;
        }

        private void OnDisable()
        {
            if (_gm == null) return;
            _gm.OnTurnPhaseChanged -= OnTurnPhaseChanged;
            _gm.AttackPhase.OnAttackTurn -= OnAttackPhaseTurn;
            _gm.FortifyPhase.OnFortifyAction -= OnFortified;
        }

        // Redeployment: the phase stays open for one more move, so take another step once this one is done.
        private void OnFortified(Actions.FortifyAction action)
        {
            if (action.Player == _player && _gm.RunsBotAI && _gm.FortifyPhase.ExtraMoves > 0
                && _botBrain.UsesHeuristic(this))
                _waiting = true;
        }


        private void OnAttackPhaseTurn()
        {
            if (!_gm.IsCurrentPlayer(_player) || !_gm.RunsBotAI)
                return;

            // The event raised as the phase starts comes before OnTurnPhaseChanged, which handles that one.
            // (Not "AttackTurn <= 1": Cease Fire on the first attack raises it again without a new turn.)
            if (_botBrain.CurrentPhase != _botBrain.AttackPhase)
                return;

            Think();
        }

        private void OnTurnPhaseChanged(IPhase oldPhase, IPhase newPhase)
        {
            if (!_gm.IsCurrentPlayer(_player)
                || _gm.GameState != GameState.Playing
                || !_gm.RunsBotAI)
                return;

            _botBrain.OnTurnPhaseChanged(oldPhase, newPhase);
            _waiting = false;
            if (newPhase is ReinforcePhase)
            {
                var plan = BotEconomy.Plan(_gm, _player);
                foreach (var action in plan)
                    ActionReader.Instance.AddAction(action);
                // MOD Reduction / Armageddon: placing now would be refused while the others answer.
                if (plan.Any(BotEconomy.StopsTheGame))
                {
                    _waiting = true;
                    return;
                }
            }
            if (newPhase is not EmptyPhase)
                Think();
        }

        // Set while the bot's next step has to wait: its queued actions haven't run yet, or someone is
        // answering a question (a card's, a defender's).
        private bool _waiting;

        private void Think()
        {
            if (_gm.PendingDecision != null)
            {
                _waiting = true;
                return;
            }
            _botBrain.HandleCommunication(this, _player);
        }

        private void Update()
        {
            if (!_waiting || _gm == null) return;
            if (!_gm.IsCurrentPlayer(_player) || _gm.GameState != GameState.Playing || !_gm.RunsBotAI
                || _gm.CurrentPhase is EmptyPhase)
            {
                _waiting = false;
                return;
            }
            if (_gm.PendingDecision != null || ActionReader.Instance.Count > 0) return;
            _waiting = false;
            Think();
        }

        // This device just started running the bots (it became the online host) in the middle of the
        // bot's turn, so the phase-change event that normally starts it has already gone by.
        public void Resume()
        {
            if (!_gm.IsCurrentPlayer(_player)
                || _gm.GameState != GameState.Playing
                || !_gm.RunsBotAI
                || _gm.CurrentPhase is EmptyPhase)
                return;

            _botBrain.OnTurnPhaseChanged(null, _gm.CurrentPhase);
            Think();
        }
    }
}