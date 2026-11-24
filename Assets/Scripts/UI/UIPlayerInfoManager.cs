using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using player;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UIPlayerInfoManager : MonoBehaviour
    {
        GameManager _gm;
        [SerializeField] private Transform _playerInfoContainer;
        [SerializeField] private UIPlayerInfo _playerInfoPrefab;

        private readonly List<UIPlayerInfo> _playerInfos = new();

        private void Awake()
        {
            _gm = GameManager.Instance;
            var layout = _playerInfoContainer != null ? _playerInfoContainer.GetComponent<VerticalLayoutGroup>() : null;
            if (layout != null)
            {
                layout.spacing = 4f;
                layout.padding = new RectOffset(2, 2, 2, 2);
                layout.childControlHeight = true;
                layout.childForceExpandHeight = false;
            }
            _gm.Players.ForEach(CreatePlayerInfo);
            _gm.OnGameStateChanged += state =>
            {
                if (state == GameState.Playing)
                {
                    OrderPlayerInfos(_gm.GetPlayersInTurnOrder());
                }
            };
            // The lower-left board lists every player. These strips repeated that list down the right edge.
            gameObject.SetActive(false);
        }

        private void OrderPlayerInfos(List<Player> playersInTurnOrder)
        {
            var ordered = playersInTurnOrder
                .OrderBy(p => p != null && p.Color != null && p.Color.name == "Black")
                .ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                var uiPlayerInfo = _playerInfos.Find(info => info.Player == ordered[i]);
                if (uiPlayerInfo != null) uiPlayerInfo.transform.SetSiblingIndex(i);
            }
        }

        private void CreatePlayerInfo(Player player)
        {
            var uiPlayerInfo = Instantiate(_playerInfoPrefab, _playerInfoContainer);
            uiPlayerInfo.Player = player;
            var le = uiPlayerInfo.gameObject.GetComponent<LayoutElement>() ?? uiPlayerInfo.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 248f;
            le.preferredHeight = 112f;
            le.minHeight = 100f;
            _playerInfos.Add(uiPlayerInfo);
        }
    }
}
