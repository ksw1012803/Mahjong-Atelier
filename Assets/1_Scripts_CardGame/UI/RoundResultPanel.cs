using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 라운드 결과 다이얼로그 v1.
    /// 
    /// 표시 내용:
    ///   - 완성 패턴 이름 + 점수
    ///   - 완성자
    ///   - 각 플레이어 누적 점수 (승리 진행도)
    ///   - 다음 라운드 버튼
    /// </summary>
    public class RoundResultPanel : MonoBehaviour
    {
        [Header("Dialog Container")]
        [SerializeField] private GameObject dialogContainer;

        [Header("Info Display")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text patternText;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private TMP_Text winnerText;

        [Header("Score Board")]
        [Tooltip("플레이어별 누적 점수 표시. 최대 4개")]
        [SerializeField] private TMP_Text[] playerScoreTexts = new TMP_Text[4];

        [Header("Next Round Button")]
        [SerializeField] private Button nextRoundButton;
        [SerializeField] private TMP_Text nextRoundButtonLabel;

        public event Action OnNextRoundClicked;

        private void Awake()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            if (nextRoundButton != null) nextRoundButton.onClick.AddListener(HandleNextRoundClicked);
        }

        private void OnDestroy()
        {
            if (nextRoundButton != null) nextRoundButton.onClick.RemoveListener(HandleNextRoundClicked);
        }

        public void Show(Player winner, CompletionResult result,
            IReadOnlyList<Player> allPlayers, int winningScore)
        {
            if (winner == null || result == null) return;

            if (titleText != null) titleText.text = "완성!";

            if (patternText != null)
                patternText.text = CompletionResult.GetPatternName(result.Pattern);

            if (pointsText != null)
                pointsText.text = $"+{result.Points} 점";

            if (winnerText != null)
            {
                string marker = winner.IsHuman ? " (나)" : "";
                winnerText.text = $"P{winner.Index}{marker}";
            }

            // 점수판
            for (int i = 0; i < playerScoreTexts.Length; i++)
            {
                var text = playerScoreTexts[i];
                if (text == null) continue;
                if (i < allPlayers.Count)
                {
                    var p = allPlayers[i];
                    text.gameObject.SetActive(true);
                    string marker = p.IsHuman ? " (나)" : "";
                    text.text = $"P{p.Index}{marker}  {p.TotalScore} / {winningScore}";
                }
                else
                {
                    text.gameObject.SetActive(false);
                }
            }

            if (nextRoundButtonLabel != null && string.IsNullOrEmpty(nextRoundButtonLabel.text))
                nextRoundButtonLabel.text = "다음 라운드";

            if (nextRoundButton != null) nextRoundButton.interactable = true;
            if (dialogContainer != null) dialogContainer.SetActive(true);
        }

        public void Hide()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
        }

        private void HandleNextRoundClicked()
        {
            Debug.Log("[RoundResultPanel] 다음 라운드 클릭");
            if (nextRoundButton != null) nextRoundButton.interactable = false;
            Hide();
            OnNextRoundClicked?.Invoke();
        }
    }
}
