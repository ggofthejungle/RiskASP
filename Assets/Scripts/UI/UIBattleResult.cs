using System;
using TurnPhases;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UIBattleResult : MonoBehaviour
    {
        private GameManager _gm;
        
        [SerializeField] private Image[] _attackDiceRenderers;
        [SerializeField] private Image[] _defenderDiceRenderers;
        [SerializeField] private Image[] _battleResultRenderers;
        
        [Space(10)]
        [SerializeField] private Sprite[] _diceSprites;

        [SerializeField] private Sprite _attackerWonSprite;
        [SerializeField] private Sprite _defenderWonSprite;
        
        
        [Space(10)]
        [SerializeField] private Color _attackerColor;
        [SerializeField] private Color _attackerWonColor;
        
        [Space(10)]
        [SerializeField] private Color _defenderColor;
        [SerializeField] private Color _defenderWonColor;


        private void Start()
        {
            _gm = GameManager.Instance;
            _gm.AttackPhase.OnAttacked += (attackResult) =>
            {
                if (_gm.CurrentPhase is AttackPhase attackPhase)
                    DisplayAttackResult(attackPhase, attackResult);
            };
        }

        private void DisplayAttackResult(AttackPhase attackPhase, AttackResult attackResult)
        {
            for (int index = 0; index < 3; index++)
            {
                var attackDiceRenderer = _attackDiceRenderers[index];
                var defenderDiceRenderer = _defenderDiceRenderers[index];
                
                attackDiceRenderer.color = _attackerColor;
                defenderDiceRenderer.color = _defenderColor;

                attackDiceRenderer.enabled = false;
                defenderDiceRenderer.enabled = false;
                _battleResultRenderers[index].enabled = false;
                
                attackDiceRenderer.sprite = null;
                defenderDiceRenderer.sprite = null;
                SetD8Tag(attackDiceRenderer, false);
                SetD8Tag(defenderDiceRenderer, false);
            }
            
            Debug.Log("Attacking troops " + attackResult.AttackingTroops);
            Debug.Log( "_attackDiceRenderers.Length " + _attackDiceRenderers.Length );
            
            Debug.Log( "_diceSprites.Length " + _diceSprites.Length );
    
            for (var index = 0; index < Math.Min(_attackDiceRenderers.Length, attackResult.AttackerRolls.Length); index++)
            {
                Debug.Log("i " + index + " " + attackResult.AttackerRolls[index]);
                var attackerRoll = attackResult.AttackerRolls[index];
                    
                _attackDiceRenderers[index].sprite = _diceSprites[attackerRoll - 1];
                _attackDiceRenderers[index].enabled = true;
                SetD8Tag(_attackDiceRenderers[index], index < attackResult.AttackerDieSides.Length && attackResult.AttackerDieSides[index] == 8);
            }
            
            Debug.Log("Defending troops " + attackResult.DefendingTroops);
            Debug.Log( "_defenderDiceRenderers.Length " + _defenderDiceRenderers.Length );

            for (var index = 0; index < Math.Min(_defenderDiceRenderers.Length, attackResult.DefenderRolls.Length); index++)
            {
                Debug.Log("i " + index + " " + attackResult.DefenderRolls[index]);
                var defenderRoll = attackResult.DefenderRolls[index];
                _defenderDiceRenderers[index].sprite = _diceSprites[defenderRoll - 1];
                _defenderDiceRenderers[index].enabled = true;
                SetD8Tag(_defenderDiceRenderers[index], index < attackResult.DefenderDieSides.Length && attackResult.DefenderDieSides[index] == 8);
            }

            var battleWidth = Math.Min(attackResult.AttackerRolls.Length, attackResult.DefenderRolls.Length);
            for (var index = 0; index < battleWidth; index++)
            {
                var attackerRoll = attackResult.AttackerRolls[index];
                var defenderRoll = attackResult.DefenderRolls[index];
                bool attackerWon = attackerRoll > defenderRoll;

                _battleResultRenderers[index].enabled = true;
                
                if (attackerWon)
                {
                    _attackDiceRenderers[index].color = _attackerWonColor;
                    _battleResultRenderers[index].sprite = _attackerWonSprite;
                }
                else
                {
                    _defenderDiceRenderers[index].color = _defenderWonColor;
                    _battleResultRenderers[index].sprite = _defenderWonSprite;
                }
            }
        }

        // A small "d8" tag on a die that was 8-sided (commander or space station), so it's clear which dice were d8.
        private static void SetD8Tag(Image die, bool isD8)
        {
            var tag = die.transform.Find("d8");
            if (tag == null)
            {
                if (!isD8) return;
                var go = new GameObject("d8", typeof(RectTransform));
                go.transform.SetParent(die.transform, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.45f, -0.05f);
                rt.anchorMax = new Vector2(1.15f, 0.40f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var bg = go.AddComponent<Image>();
                bg.color = new Color(0.92f, 0.45f, 0.05f);
                bg.raycastTarget = false;
                var text = new GameObject("t", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
                text.transform.SetParent(go.transform, false);
                var trt = text.rectTransform;
                trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
                text.text = "d8";
                text.fontSize = 14;
                text.fontStyle = TMPro.FontStyles.Bold;
                text.alignment = TMPro.TextAlignmentOptions.Center;
                text.color = Color.white;
                text.raycastTarget = false;
                tag = go.transform;
            }
            tag.gameObject.SetActive(isD8 && die.enabled);
        }
    }
}