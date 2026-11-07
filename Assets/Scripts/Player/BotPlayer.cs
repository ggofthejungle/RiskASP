using System.Collections.Generic;
using TurnPhases;
using UnityEngine;

namespace player
{
    [RequireComponent(typeof(Player))]
    public class BotPlayer : MonoBehaviour
    {
        public BotConfiguration BotConfiguration => _botConfiguration;
        [SerializeField] private BotConfiguration _botConfiguration;

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
        }


        private void OnAttackPhaseTurn()
        {
            if (!_gm.IsCurrentPlayer(_player) || !_gm.RunsBotAI)
                return;

            // skip first attack turn, since it's handled by
            // OnTurnPhaseChanged
            if (_gm.AttackPhase.AttackTurn <= 1)
                return;


            _botBrain.HandleCommunication(this, _player);
        }

        private void OnTurnPhaseChanged(IPhase oldPhase, IPhase newPhase)
        {
            if (!_gm.IsCurrentPlayer(_player)
                || _gm.GameState != GameState.Playing
                || !_gm.RunsBotAI)
                return;

            if (newPhase is ReinforcePhase)
                foreach (var action in BotEconomy.Plan(_gm, _player))
                    ActionReader.Instance.AddAction(action);

            _botBrain.OnTurnPhaseChanged(oldPhase, newPhase);
            if (newPhase is not EmptyPhase)
                _botBrain.HandleCommunication(this, _player);
        }

        // This device just started running the bots (it became the online host) in the middle of the
        // bot's turn, so the phase-change event that normally starts it has already gone by.
        public void Resume()
        {
            if (!_gm.IsCurrentPlayer(_player)
                || _gm.GameState != GameState.Playing
                || !_gm.RunsBotAI
                || _gm.PendingDecision != null
                || _gm.CurrentPhase is EmptyPhase)
                return;

            _botBrain.OnTurnPhaseChanged(null, _gm.CurrentPhase);
            _botBrain.HandleCommunication(this, _player);
        }
    }
}