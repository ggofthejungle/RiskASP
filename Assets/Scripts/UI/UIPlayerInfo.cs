using player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UIPlayerInfo : MonoBehaviour
    {
        [SerializeField] private Player _player;

        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _troopsText;
        [SerializeField] private TMP_Text _territoriesText;
        [SerializeField] private TMP_Text _cardsText;
        [SerializeField] private TMP_Text _energyText;
        
        [SerializeField] private Image _colorImage;

        [SerializeField] private RectTransform _backgroundRectTransform;
        [SerializeField] private Image _backgroundImage;
        [SerializeField] private float _backgroundOffsetOnCurrentPlayer = 20f;
        private float _backgroundStartOffset;
        
        private Color _defaultBackgroundColor;
        [SerializeField] private Color _currentPlayerBackgroundColor;
        
        public Player Player
        {
            get => _player;
            set => SetPlayer(value);
        }


        private void Awake()
        {
            Grow(_nameText, 26f);
            Grow(_troopsText, 22f);
            Grow(_territoriesText, 22f);
            Grow(_cardsText, 22f);
            Grow(_energyText, 22f);
            if (_player != null)
                SetPlayer(_player);
        }

        static void Grow(TMP_Text text, float size)
        {
            if (text == null) return;
            text.enableAutoSizing = false;
            if (text.fontSize < size) text.fontSize = size;
        }

        private void SetPlayer(Player value)
        {
            _defaultBackgroundColor = _backgroundImage.color;
            _player = value;
            _nameText.text = _player.Name;

            UpdatePlayerData();
            _player.OnCardsChanged += UpdateCardsData;
            _player.OnEliminated += (_) => _colorImage.color = _player.Color.Disabled;

            _backgroundStartOffset = _backgroundRectTransform.anchoredPosition.x;
            
            var gameManager = GameManager.Instance;
           
            OnPlayerTurnChanged(gameManager.CurrentPlayer);
            
            gameManager.OnPlayerTurnChanged += (_, newPlayer) => OnPlayerTurnChanged(newPlayer);
            gameManager.AttackPhase.OnAttacked += (_) => UpdatePlayerData();
            gameManager.OnGameStateChanged += (_) => UpdatePlayerData();
            gameManager.ReinforcePhase.OnTroopsPlaced += (_) => UpdatePlayerData();
        }

        private void OnPlayerTurnChanged(Player newPlayer)
        {
            if (newPlayer == _player)
            {
                _colorImage.color = _player.Color.Selected;
                var pos = Vector2.right * (_backgroundOffsetOnCurrentPlayer + _backgroundStartOffset);
                _backgroundRectTransform.anchoredPosition = pos;
                _backgroundImage.color = _currentPlayerBackgroundColor;
            }
            else
            {
                _colorImage.color = _player.Color.Normal;
                var pos = Vector2.right * _backgroundStartOffset;
                _backgroundRectTransform.anchoredPosition = pos;
                _backgroundImage.color = _defaultBackgroundColor;
            }

            if (_player.IsDead())
                _colorImage.color = _player.Color.Disabled;
        }

        // Cards, hires and energy change units and energy outside the events above.
        private (int troops, int territories, int energy, int cards) _shown = (-1, -1, -1, -1);
        private void LateUpdate()
        {
            if (_player == null) return;
            var now = (_player.GetTroopsCountInTerritories(), _player.GetTerritoryCount(), _player.GetEnergy(), _player.Cards.Count);
            if (now != _shown) UpdatePlayerData();
        }

        private void UpdatePlayerData()
        {
            _shown = (_player.GetTroopsCountInTerritories(), _player.GetTerritoryCount(), _player.GetEnergy(), _player.Cards.Count);
            _troopsText.text = _shown.troops.ToString();
            _territoriesText.text = _shown.territories.ToString();
            _energyText.text = _shown.energy.ToString();
            UpdateCardsData();
        }

        private void UpdateCardsData()
        {
            _cardsText.text = _player.Cards.Count.ToString();
        }
    }
}