using System;
using player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace UI
{
    public class UIPlayerCreationSlot : MonoBehaviour
    {

        [SerializeField] private UIPlayerCreationManager _uiPlayerCreationManager;
        public PlayerCreationConfiguration PlayerCreationConfiguration { get; private set; }

        public bool PlayerCreated => PlayerCreationConfiguration != null;
        
        
        [SerializeField] private Button _addPlayerButton;
        [SerializeField] private Button _removePlayerButton;
        [SerializeField] private TMP_InputField _playerNameInputField;
        [SerializeField] private TMP_Dropdown _playerColorDropdown;
        [SerializeField] private TMP_Dropdown _playerConfigurationDropdown;
        [SerializeField] private Image _playerColorImage;
        
        public Action<PlayerCreationConfiguration> OnPlayerAdded;
        public Action<PlayerCreationConfiguration> OnPlayerRemove;
        
        public Action<PlayerCreationConfiguration> OnPlayerCreationConfigurationChanged;
        [SerializeField] private GameObject _playerCreatedGfx;
        [SerializeField] private GameObject _playerNotCreatedGfx;

        private void Awake()
        {
            if(_uiPlayerCreationManager == null)
                _uiPlayerCreationManager = FindAnyObjectByType<UIPlayerCreationManager>();
            
            PlayerCreationConfiguration = null;
            
            _playerCreatedGfx.SetActive(PlayerCreated);
            _playerNotCreatedGfx.SetActive(!PlayerCreated);
            
            GenerateDropdownItems();
            
            
            _addPlayerButton.onClick.AddListener(AddPlayer);
            _removePlayerButton.onClick.AddListener(RemovePlayer);
            _playerNameInputField.onValueChanged.AddListener(OnPlayerNameChanged);
            _playerColorDropdown.onValueChanged.AddListener(OnPlayerColorChanged);
            _playerConfigurationDropdown.onValueChanged.AddListener(OnPlayerConfigurationChanged);
        }

        private void GenerateDropdownItems()
        {
            var playerConfigurations = _uiPlayerCreationManager.PlayerConfigurations;
            var playerColors = _uiPlayerCreationManager.PlayerColors;
            
            _playerConfigurationDropdown.ClearOptions();
            _playerColorDropdown.ClearOptions();
            
            foreach (var playerConfiguration in playerConfigurations)
                _playerConfigurationDropdown.options.Add(new TMP_Dropdown.OptionData(playerConfiguration.Name));

            foreach (var playerColor in playerColors)
                _playerColorDropdown.options.Add(new TMP_Dropdown.OptionData(playerColor.name));
            
            _playerConfigurationDropdown.RefreshShownValue();
            _playerColorDropdown.RefreshShownValue();
        }

        private void OnPlayerConfigurationChanged(int index)
        {
            var playerConfiguration = _uiPlayerCreationManager.PlayerConfigurations[index];
            PlayerCreationConfiguration.PlayerConfiguration = playerConfiguration;
            OnPlayerCreationConfigurationChanged?.Invoke(PlayerCreationConfiguration);
        }

        private void OnPlayerColorChanged(int index)
        {
            var playerColor = _uiPlayerCreationManager.PlayerColors[index];
            PlayerCreationConfiguration.PlayerColor = playerColor;
            _playerColorImage.color = playerColor.Selected;
            OnPlayerCreationConfigurationChanged?.Invoke(PlayerCreationConfiguration);
        }

        private void OnPlayerNameChanged(string name)
        {
            if(!PlayerCreated) return;

            PlayerCreationConfiguration.PlayerName = name;
            OnPlayerCreationConfigurationChanged?.Invoke(PlayerCreationConfiguration);
        }


        private void AddPlayer() => AddPlayer(-1, null);

        // colorIndex < 0 = random. configIndex < 0 = Human if present, else 0.
        public void AddPlayer(int colorIndex, string playerName, int configIndex = -1)
        {
            if (PlayerCreated) return;

            var configs = _uiPlayerCreationManager.PlayerConfigurations;
            var colors = _uiPlayerCreationManager.PlayerColors;
            if (configs == null || configs.Length == 0 || colors == null || colors.Length == 0) return;

            if (configIndex < 0 || configIndex >= configs.Length)
            {
                configIndex = 0;
                for (int i = 0; i < configs.Length; i++)
                    if (configs[i] != null && configs[i].Name == "Human") { configIndex = i; break; }
            }
            if (colorIndex < 0 || colorIndex >= colors.Length)
                colorIndex = Random.Range(0, colors.Length);

            var playerConfiguration = configs[configIndex];
            var playerColor = colors[colorIndex];
            PlayerCreationConfiguration = new PlayerCreationConfiguration
            {
                PlayerColor = playerColor,
                PlayerConfiguration = playerConfiguration,
                PlayerName = string.IsNullOrEmpty(playerName)
                    ? "Player " + (_uiPlayerCreationManager.PlayerCount + 1)
                    : playerName,
            };

            _playerNameInputField.text = PlayerCreationConfiguration.PlayerName;
            _playerColorDropdown.value = colorIndex;
            _playerConfigurationDropdown.value = configIndex;
            _playerColorImage.color = playerColor.Selected;

            _playerCreatedGfx.SetActive(PlayerCreated);
            _playerNotCreatedGfx.SetActive(!PlayerCreated);

            OnPlayerAdded?.Invoke(PlayerCreationConfiguration);
        }

        public void ClearIfCreated()
        {
            if (!PlayerCreated) return;
            OnPlayerRemove?.Invoke(PlayerCreationConfiguration);
            PlayerCreationConfiguration = null;
            _playerCreatedGfx.SetActive(false);
            _playerNotCreatedGfx.SetActive(true);
        }

        private void RemovePlayer()
        {
            Debug.Log("RemovePlayer");
            ClearIfCreated();
        }

    }
}
