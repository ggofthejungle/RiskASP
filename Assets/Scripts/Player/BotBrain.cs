using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using EmbASP;
using EmbASP.predicates;
using it.unical.mat.embasp.@base;
using it.unical.mat.embasp.languages.asp;
using it.unical.mat.embasp.platforms.desktop;
using it.unical.mat.embasp.specializations.dlv2.desktop;
using Map;
using TurnPhases;
using TurnPhases.AI;
using UnityEngine;

namespace player
{
    public class BotBrain : MonoBehaviour
    {
        private GameManager _gm;


        [SerializeField] private string _constantsBrainPath;
        [Space(10)] [SerializeField] private string _reinforceBrainPath;
        [SerializeField] private string _attackBrainPath;
        [SerializeField] private string _fortifyBrainPath;

        public IAIPhase CurrentPhase { get; private set; }
        private Handler _handler;


        public ReinforceAIPhase ReinforcePhase { get; private set; }
        public AttackAIPhase AttackPhase { get; private set; }
        public FortifyAIPhase FortifyPhase { get; private set; }
        public EmptyAIPhase EmptyPhase { get; private set; }


        private void Awake()
        {
            Debug.Log("BotBrain Awake");

            _gm = GameManager.Instance;
            SetupPhases();
            _handler = LoadExecutable();
            RegisterClassesToMapper();
        }

        private void OnEnable()
        {
            Debug.Log("BotBrain OnEnable");
        }

        private void Start()
        {
            Debug.Log("BotBrain Start");
        }

        private void SetupPhases()
        {
            var ar = ActionReader.Instance;
            var tr = TerritoryRepository.Instance;

            ReinforcePhase = new ReinforceAIPhase(_gm, ar, tr);
            AttackPhase = new AttackAIPhase(_gm, ar, tr);
            FortifyPhase = new FortifyAIPhase(_gm, ar, tr);
            EmptyPhase = new EmptyAIPhase();
            CurrentPhase = EmptyPhase;
        }

        // called from BotPlayer
        public void OnTurnPhaseChanged(IPhase oldPhase, IPhase newPhase)
        {
            var phase = TurnPhaseToAIPhase(newPhase);
            SetCurrentPhase(phase);
        }

        private IAIPhase TurnPhaseToAIPhase(IPhase newPhase)
        {
            return newPhase switch
            {
                TurnPhases.ReinforcePhase => ReinforcePhase,
                TurnPhases.AttackPhase => AttackPhase,
                TurnPhases.FortifyPhase => FortifyPhase,
                _ => EmptyPhase
            };
        }

        private void SetCurrentPhase(IAIPhase phase)
        {
            CurrentPhase = phase;
        }


        public void HandleCommunication(Player player)
        {
            Debug.Log("BotBrain: HandleCommunication");

            InputProgram inputProgram = CreateProgram();
            CurrentPhase.OnRequest(player, inputProgram);
            _handler.AddProgram(inputProgram);
            _handler.StartAsync(new PhasesCallback(this, player, inputProgram, _handler));
        }

        private class PhasesCallback : ICallback
        {
            private readonly BotBrain _botBrain;
            private readonly Player _player;
            private readonly InputProgram _inputProgram;
            private readonly Handler _handler;

            public PhasesCallback(BotBrain botBrain, Player player, InputProgram inputProgram, Handler handler)
            {
                _botBrain = botBrain;
                _player = player;
                _inputProgram = inputProgram;
                _handler = handler;
            }

            public void Callback(Output output)
            {
                // _handler.RemoveProgram(_inputProgram);

                // todo: uncomment when using optimal answer sets
                
                
                var answerSets = (AnswerSets)output;
                // var optimalAnswerSet = answerSets.GetOptimalAnswerSets();

                AnswerSet answerSet;
                // if (optimalAnswerSet.Count > 0)
                //     answerSet = optimalAnswerSet[0];
                // else 
                if (answerSets.Answersets.Count > 0)
                    answerSet = answerSets.Answersets[0];
                else
                {
                    Debug.LogError("BotBrain: No answer set found");
                    return;
                }

                Debug.Log("BotBrain: Callback\nanswerSet: " + answerSet + "\nphase: " + _botBrain.CurrentPhase);
                _botBrain.CurrentPhase.OnResponse(_player, answerSet);
            }
        }

        public InputProgram CreateProgram()
        {
            string currentBrain = CurrentPhase switch
            {
                ReinforceAIPhase => _reinforceBrainPath,
                AttackAIPhase => _attackBrainPath,
                FortifyAIPhase => _fortifyBrainPath,
                _ => ""
            };

            InputProgram inputProgram = new ASPInputProgram();
            LoadConstants(inputProgram);

            LoadBrain(currentBrain, inputProgram);

            LoadPhaseInfo(inputProgram);

            return inputProgram;
        }

        private void LoadPhaseInfo(InputProgram inputProgram)
        {
            var tr = TerritoryRepository.Instance;
            
            // turn info
            var turn = new TurnPredicate(_gm.Turn, _gm.CurrentPlayer.Name);
            inputProgram.AddObjectInput(turn);
            
            // territory control
            var territoryControls = TerritoryControlPredicate.FromTerritoriesAsObjects(_gm.Turn, tr.Territories);
            inputProgram.AddObjectsInput(territoryControls);
            
            // players
            foreach (var player in _gm.Players) 
                inputProgram.AddObjectInput(new PlayerPredicate(player.Name));
            
        }


        private void LoadConstants(InputProgram inputProgram)
        {
            if (_constantsBrainPath == "")
                return;

            if (!File.Exists(_constantsBrainPath))
                throw new IOException("Constants file not found");
            string str = File.ReadAllText(_constantsBrainPath);
            inputProgram.AddProgram(str);
        }

        private void LoadBrain(string brainPath, InputProgram inputProgram)
        {
            if (brainPath == "")
                throw new Exception("Brain path not set");
            if (!File.Exists(brainPath))
                throw new IOException("Brain file not found");
            string str = File.ReadAllText(brainPath);
            inputProgram.AddProgram(str);
        }


        private Handler LoadExecutable()
        {
            var separator = Path.DirectorySeparatorChar;
            string executablePath;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                executablePath = $"Executables{separator}dlv2.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                executablePath = $"Executables{separator}dlv2-linux";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                executablePath = $"Executables{separator}dlv2-mac";
            else throw new Exception("OS not supported");


            return new DesktopHandler(new DLV2DesktopService(executablePath));
        }


        void RegisterClassesToMapper()
        {
            // get all classes in the namespace EmbASP.predicates
            var predicateClasses = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.Namespace == "EmbASP.predicates")
                .ToList();

            // register all predicate classes
            foreach (var predicateClass in predicateClasses)
            {
                ASPMapper.Instance.RegisterClass(predicateClass);
            }

            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.Player));
            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.AfterAttackMove));
            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.Attack));
            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.AttackResult));
            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.Move));
            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.Place));
            //
            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.StopAttacking));
            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.TerritoryControl));
            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.Turn));
            // ASPMapper.Instance.RegisterClass(typeof(EmbASP.predicates.UnitsToPlace));
        }
    }
}