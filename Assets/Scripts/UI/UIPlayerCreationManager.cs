using System;
using player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class UIPlayerCreationManager : MonoBehaviour
    {
        [SerializeField] private UIPlayerCreationSlot[] _slots;
        [SerializeField] private Button _startGameButton;
        [SerializeField] private StartingPlayersConfiguration _startingPlayersConfiguration;

        [SerializeField] public PlayerConfiguration[] PlayerConfigurations;
        [SerializeField] public PlayerColor[] PlayerColors;
        
        public int PlayerCount => _startingPlayersConfiguration.PlayersConfiguration.Count;
        
        
        // The C# bot (MM-2). Loaded here so the seat picker offers it without a scene edit.
        public const string StandardBotName = "Standard BOT";
        const string StandardBotResource = "Bots/standard_bot_conf";

        private void Awake()
        {
            AddStandardBot();
            _startingPlayersConfiguration.PlayersConfiguration.Clear();
            
            _startGameButton.interactable = PlayerCount >= 2;
            _startGameButton.onClick.AddListener(StartGame);

            foreach (var uiPlayerCreationSlot in _slots)
            {
                uiPlayerCreationSlot.OnPlayerAdded += OnPlayerAdded;
                uiPlayerCreationSlot.OnPlayerRemove += OnPlayerRemove;
            }
        }

        private void Start()
        {
            // The main menu fills the seats for the mode the player picked.
            // If the menu isn't running, keep the old two-human hot-seat default.
            if (MainMenu.IsRunning) return;
            if (_slots == null || _slots.Length < 2 || PlayerCount > 0) return;
            var red = ColorIndex("Red");
            var blue = ColorIndex("Blue");
            _slots[0].AddPlayer(red, "Player 1");
            _slots[1].AddPlayer(blue, "Player 2");
            _startGameButton.interactable = PlayerCount >= 2;
        }

        public void Prepare(MatchKind kind)
        {
            ClearAll();
            if (kind == MatchKind.HotSeat)
            {
                AddDefault(0, "Red", "Player 1", "Human");
                AddDefault(1, "Blue", "Player 2", "Human");
            }
            else
            {
                AddDefault(0, "Red", "You", "Human");
                AddDefault(1, "Blue", "Bot", StandardBotName);
            }
            _startGameButton.interactable = PlayerCount >= 2;
        }

        static readonly string[] OnlineColors = { "Red", "Blue", "Green", "Yellow", "Purple" };

        // Online: one player per room seat, in seat order. Written straight to the starting configuration
        // (the seat picker stays closed), so every device builds the same Players list.
        public void PrepareOnline(System.Collections.Generic.IReadOnlyList<string> names,
            System.Collections.Generic.IReadOnlyList<bool> isBot = null)
        {
            ClearAll();
            var list = _startingPlayersConfiguration.PlayersConfiguration;
            list.Clear();
            var human = PlayerConfigurations[ConfigIndex("Human")];
            // The host may be a phone: online bots play the C# way.
            var bot = PlayerConfigurations[ConfigIndex(StandardBotName)];
            for (int i = 0; i < names.Count; i++)
            {
                list.Add(new PlayerCreationConfiguration
                {
                    PlayerConfiguration = isBot != null && i < isBot.Count && isBot[i] ? bot : human,
                    PlayerName = names[i],
                    PlayerColor = PlayerColors[ColorIndex(OnlineColors[i % OnlineColors.Length])],
                });
            }
        }

        // Also called by the seats, whose Awake (which fills their dropdowns) may run before this one's.
        public void AddStandardBot()
        {
            var standard = Resources.Load<BotConfiguration>(StandardBotResource);
            if (standard == null) return;
            var list = PlayerConfigurations ?? Array.Empty<PlayerConfiguration>();
            if (Array.IndexOf(list, standard) >= 0) return;
            Array.Resize(ref list, list.Length + 1);
            list[list.Length - 1] = standard;
            PlayerConfigurations = list;
        }

        public void ClearAll()
        {
            if (_slots == null) return;
            foreach (var slot in _slots)
                if (slot != null) slot.ClearIfCreated();
        }

        void AddDefault(int slot, string colorName, string playerName, string configName)
        {
            if (_slots == null || slot < 0 || slot >= _slots.Length || _slots[slot] == null) return;
            _slots[slot].AddPlayer(ColorIndex(colorName), playerName, ConfigIndex(configName));
        }

        int ConfigIndex(string configName)
        {
            if (PlayerConfigurations == null) return 0;
            for (int i = 0; i < PlayerConfigurations.Length; i++)
                if (PlayerConfigurations[i] != null && PlayerConfigurations[i].Name == configName) return i;
            if (configName != "Human")
            {
                for (int i = 0; i < PlayerConfigurations.Length; i++)
                    if (PlayerConfigurations[i] is BotConfiguration) return i;
            }
            return 0;
        }

        private int ColorIndex(string colorName)
        {
            if (PlayerColors == null) return 0;
            for (int i = 0; i < PlayerColors.Length; i++)
                if (PlayerColors[i] != null && PlayerColors[i].name == colorName) return i;
            return 0;
        }

        private void OnPlayerRemove(PlayerCreationConfiguration playerConfiguration)
        {
            _startingPlayersConfiguration.PlayersConfiguration.Remove(playerConfiguration);
            _startGameButton.interactable = PlayerCount >= 2;
        }

        private void OnPlayerAdded(PlayerCreationConfiguration playerConfiguration)
        {
            _startingPlayersConfiguration.PlayersConfiguration.Add(playerConfiguration);
            _startGameButton.interactable = PlayerCount >= 2;
        }


        // Continue: write the saved seats onto the starting configuration the match scene reads.
        public bool ApplySavedSeats(string[] seats)
        {
            AddStandardBot();
            if (seats == null || seats.Length < 2 || _startingPlayersConfiguration == null) return false;
            var list = _startingPlayersConfiguration.PlayersConfiguration;
            list.Clear();
            foreach (var seat in seats)
            {
                var name = MatchSave.SeatName(seat);
                var color = MatchSave.SeatColor(seat);
                var config = MatchSave.SeatConfig(seat);
                if (string.IsNullOrEmpty(name)) return false;
                list.Add(new PlayerCreationConfiguration
                {
                    PlayerConfiguration = PlayerConfigurations[ConfigIndex(string.IsNullOrEmpty(config) ? "Human" : config)],
                    PlayerName = name,
                    PlayerColor = PlayerColors[ColorIndex(string.IsNullOrEmpty(color) ? "Red" : color)],
                });
            }
            return list.Count >= 2;
        }

        private void StartGame()
        {
            MatchSave.ClearPending();
            MatchSession.ArmForNextLoad();
            SceneManager.LoadScene("Main");
        }
    }
}
