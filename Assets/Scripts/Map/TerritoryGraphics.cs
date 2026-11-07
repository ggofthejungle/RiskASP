using System;
using System.Collections.Generic;
using Actions;
using Expansions;
using player;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

namespace Map
{
    public class TerritoryGraphics : MonoBehaviour
    {
        [SerializeField] private Territory _territory;
        [SerializeField] private TerritorySelection _territorySelection;
        
        [SerializeField] private SpriteRenderer _territorySpriteRenderer;
        [SerializeField] private SpriteRenderer _troopsCountBackgroundSpriteRenderer;
        [SerializeField] private TMP_Text _troopsCountText;
        [SerializeField] private TMP_Text _commandersText;
        [SerializeField] private TMP_Text _spaceStationText;
        [SerializeField] private TroopCountChangedEffect _troopsChangedEffectPrefab;
        [SerializeField] private Transform _troopsChangedEffectParent;
        [SerializeField] public TMP_Text _territoryNameText;
        
        private PlayerColor _playerColor;

        private bool _isHovered;
        private bool _isSelected;
        private bool _isDisabled;
        
        private void Awake()
        {
            _territory.OnStateChanged += UpdateGraphics;
            if(_territorySelection != null)
                _territorySelection.OnStateChanged += OnStateChangedTerritorySelection;
            else 
                Debug.LogWarning("TerritorySelection is null");

            _territoryNameText.text = _territory.Name.Replace('_', ' ');


            _territory.OnTroopsChanged += OnTroopsChanged;
            _territory.OnCommandersChanged += OnCommandersChanged;
            _territory.OnSpaceStationChanged += OnSpaceStationChanged;
        }



        // No owner yet (the take-turns setup) or devastated (out of the game).
        static PlayerColor _unclaimed, _devastated;
        static PlayerColor Neutral(Color normal)
        {
            var c = ScriptableObject.CreateInstance<PlayerColor>();
            c.Normal = normal;
            c.Highlight = Color.Lerp(normal, Color.white, 0.25f);
            c.Selected = Color.Lerp(normal, Color.white, 0.4f);
            c.Disabled = Color.Lerp(normal, Color.black, 0.3f);
            return c;
        }

        PlayerColor ColorOfOwner()
        {
            if (_territory.Devastated) return _devastated ??= Neutral(new Color(0.16f, 0.15f, 0.14f, 1f));
            if (_territory.Owner == null || _territory.Owner.Color == null)
                return _unclaimed ??= Neutral(new Color(0.62f, 0.64f, 0.68f, 1f));
            return _territory.Owner.Color;
        }

        private void Start()
        {
            _playerColor = ColorOfOwner();
            UpdateGraphics();
            // Commanders and stations are shown by Map/MapBadges now (clear icons); hide the old debug texts.
            if (_commandersText != null) _commandersText.gameObject.SetActive(false);
            if (_spaceStationText != null) _spaceStationText.gameObject.SetActive(false);
        }


        private void OnTroopsChanged(int oldTroops, int newTroops)
        {
            if(GameManager.Instance.GameState == GameState.Setup)
                return;
            
            var difference = newTroops - oldTroops;
            var troopCountChangedEffect = InstantiateTroopCountChangedEffect();
            troopCountChangedEffect.SetTroopText(difference);
        }
        private void OnCommandersChanged(List<Commander> oldCommandersList, List<Commander> newCommandersList)
        {
            //TODO: implement: check old vs new 
            
            //I guess re-enable this once we are done testing?
            /*if(GameManager.Instance.GamePhase == GamePhase.Setup)
                return;*/

            _commandersText.text = "";
            // loop thru commanders list and add to _commandersText
            foreach (var commander in newCommandersList)
            {
                if (commander.CommanderType == CommanderType.LandCommander)
                    _commandersText.text += " " + "L";
                else if (commander.CommanderType == CommanderType.Diplomat)
                    _commandersText.text += " " + "D";
                else if (commander.CommanderType == CommanderType.SpaceCommander)
                    _commandersText.text += " " + "S";
                else if (commander.CommanderType == CommanderType.NuclearCommander)
                    _commandersText.text += " " + "N";
                else if (commander.CommanderType == CommanderType.NavalCommander)
                    _commandersText.text += " " + "W";
            }

        }

        private void OnSpaceStationChanged(bool s)
        {
            if (s)
                _spaceStationText.text = "YESSSSSS *";
            else
                _spaceStationText.text = "NO SPACE STATION";
        }


        private void OnStateChangedTerritorySelection(TerritorySelection territorySelection)
        {
            _isSelected = territorySelection.IsSelected;
            _isHovered = territorySelection.IsHovered;
            _isDisabled = territorySelection.IsDisabled;
            UpdateColor();
        }
        
        private TroopCountChangedEffect InstantiateTroopCountChangedEffect()
        {
            var troopCountChangedEffect = Instantiate(_troopsChangedEffectPrefab, _troopsChangedEffectParent);
            troopCountChangedEffect.SetTerritory(_territory);
            return troopCountChangedEffect;
        }


        private void UpdateGraphics()
        {
            _playerColor = ColorOfOwner();
            UpdateColor();
            SetTroopsCount(_territory.Troops);
            var showCount = !_territory.Devastated && _territory.Owner != null;
            _troopsCountText.gameObject.SetActive(showCount);
            _troopsCountBackgroundSpriteRenderer.gameObject.SetActive(showCount);
            if (_territory.Devastated)
            {
                _territoryNameText.text = _territory.Name.Replace('_', ' ') + "\n<size=70%>devastated</size>";
                _territoryNameText.color = new Color(0.72f, 0.70f, 0.66f, 1f);
            }
            //SetCommanderAndSpaceStationLabels() //if I knew what string to put in parentheses
        }

        private void UpdateColor()
        {
            if (_isSelected)
                SetColor(_playerColor.Selected);
            else if(_isDisabled)
                SetColor(_playerColor.Disabled);
            else if (_isHovered)
                SetColor(_playerColor.Highlight);
            else
                SetColor(_playerColor.Normal);
            _troopsCountBackgroundSpriteRenderer.color = _playerColor.Normal;
        }

        private void SetTroopsCount(int territoryTroops)
        {
            _troopsCountText.text = territoryTroops.ToString();
        }
        
        private void SetColor(Color color)
        {
            _territorySpriteRenderer.color = color;
        }
    }
}