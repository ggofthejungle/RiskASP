using System;
using player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class UIGameOver : MonoBehaviour
    {
        private GameManager _gameManager;
        
        [SerializeField] private Image _backgroundTintImage;
        [SerializeField] private TMP_Text _gameOverInfoText;

        private void Awake()
        {
            
            _gameManager = GameManager.Instance;
            _gameManager.OnGameStateChanged += OnGamePhaseChanged;
            gameObject.SetActive(false);
        }

        private void OnGamePhaseChanged(GameState state)
        {
            if (state == GameState.Over) 
                GameOver();
        }

        private void GameOver()
        {
            Player winner = _gameManager.Winner;
            if (winner == null) return;

            string message = $"CONGRATULATIONS!\n{winner.Name} wins after {System.Math.Min(_gameManager.Year, GameManager.MaxYears)} years!\n";
            var place = 1;
            foreach (var player in _gameManager.FinalRanking)
                message += player.IsDead()
                    ? $"{player.Name}: eliminated\n"
                    : $"{place++}. {player.Name}: {Expansions.Scoring.ScoreOf(player)} points\n";

            _gameOverInfoText.text = message;
            _backgroundTintImage.gameObject.SetActive(true);
            
            gameObject.SetActive(true);
        }
    }
}
