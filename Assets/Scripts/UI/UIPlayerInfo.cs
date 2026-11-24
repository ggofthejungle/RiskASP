using player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    public class UIPlayerInfo : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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
            Grow(_nameText, 16f);
            Grow(_troopsText, 20f);
            Grow(_territoriesText, 20f);
            Grow(_cardsText, 20f);
            Grow(_energyText, 18f);
            LayoutStats();
            EnsureHover();
            if (_player != null)
                SetPlayer(_player);
        }

        static void Grow(TMP_Text text, float size)
        {
            if (text == null) return;
            text.enableAutoSizing = false;
            text.fontSize = size;
        }

        GameObject _hint;
        TMP_Text _hintText;

        void LayoutStats()
        {
            var column = GetComponentInChildren<VerticalLayoutGroup>(true);
            if (column == null) return;
            var rt = column.GetComponent<RectTransform>();
            DestroyImmediate(column);
            rt.anchorMin = new Vector2(0.04f, 0.04f);
            rt.anchorMax = new Vector2(0.98f, 0.70f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var grid = rt.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 1;
            grid.cellSize = new Vector2(210f, 44f);
            grid.spacing = new Vector2(0f, 0f);
            grid.childAlignment = TextAnchor.MiddleRight;
            grid.padding = new RectOffset(4, 8, 4, 4);
            Hide(rt, "territories");
            Hide(rt, "energy");
            Hide(rt, "cards");
            TroopNumberOnly(rt);
            Tag(rt, "troops", "Troops", "units on the map");
        }

        // The card icon was sharing this strip with the troop count and the two collided.
        static void TroopNumberOnly(Transform root)
        {
            Transform row = null;
            foreach (Transform child in root)
                if (child.name.IndexOf("troops", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    row = child;
            if (row == null) return;
            foreach (Transform child in row)
            {
                if (child.GetComponent<TMP_Text>() != null) continue;
                child.gameObject.SetActive(false);
            }
            foreach (Transform child in row)
            {
                var text = child.GetComponent<TMP_Text>();
                if (text == null) continue;
                var box = text.rectTransform;
                box.anchorMin = Vector2.zero;
                box.anchorMax = Vector2.one;
                box.offsetMin = new Vector2(8f, 0f);
                box.offsetMax = new Vector2(-8f, 0f);
                text.alignment = TextAlignmentOptions.MidlineRight;
            }
        }

        static void Hide(Transform root, string token)
        {
            foreach (Transform child in root)
                if (child.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    child.gameObject.SetActive(false);
        }

        void Tag(Transform root, string token, string title, string meaning)
        {
            Transform row = null;
            foreach (Transform child in root)
                if (child.name.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    row = child;
            if (row == null) return;
            var hit = row.GetComponent<Image>();
            if (hit == null)
            {
                hit = row.gameObject.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0.06f);
            }
            hit.raycastTarget = true;
            var tip = row.gameObject.AddComponent<StatTip>();
            tip.Card = this;
            tip.Title = title;
            tip.Meaning = meaning;
        }

        void EnsureHover()
        {
            var hit = GetComponent<Image>();
            if (hit == null)
            {
                hit = gameObject.AddComponent<Image>();
                hit.color = new Color(0f, 0f, 0f, 0f);
            }
            hit.raycastTarget = true;

            var card = TurnHud.NewImage("Hint", transform, TurnHud.PopupBorder);
            card.raycastTarget = false;
            var rt = card.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-8f, 0f);
            rt.sizeDelta = new Vector2(260f, 78f);
            var fill = TurnHud.FramePopup(card, TurnHud.PopupFill);
            fill.raycastTarget = false;
            _hintText = TurnHud.NewText("HintText", fill.transform, 16, FontStyles.Normal, TextAlignmentOptions.Left);
            TurnHud.Stretch(_hintText.rectTransform);
            _hintText.margin = new Vector4(12f, 8f, 12f, 8f);
            _hint = card.gameObject;
            _hint.SetActive(false);
        }

        public void ShowStat(string title, string meaning)
        {
            if (_hint == null || _hintText == null) return;
            var who = _player != null ? _player.Name : "Player";
            _hintText.text = $"<b>{who}</b>\n{title} — {meaning}";
            _hint.SetActive(true);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_hint == null || _hintText == null) return;
            var who = _player != null ? _player.Name : "Player";
            _hintText.text = $"<b>{who}</b>\nHover a number to see what it is.";
            _hint.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_hint != null) _hint.SetActive(false);
        }

        sealed class StatTip : MonoBehaviour, IPointerEnterHandler
        {
            public UIPlayerInfo Card;
            public string Title;
            public string Meaning;
            public void OnPointerEnter(PointerEventData eventData) => Card?.ShowStat(Title, Meaning);
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