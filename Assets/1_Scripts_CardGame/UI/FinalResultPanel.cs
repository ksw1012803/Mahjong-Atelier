using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using CardGame.Core;
using MahjongAtelier.Core; // SceneReference 재활용

namespace CardGame.UI
{
    /// <summary>
    /// 게임 최종 결과 다이얼로그 v1.
    /// 
    /// 표시:
    ///   - 승자
    ///   - 최종 순위 (점수 순)
    ///   - 확인 버튼 (로비로 복귀)
    ///   - 다시 시작 버튼 (같은 씬에서 새 게임)
    /// </summary>
    public class FinalResultPanel : MonoBehaviour
    {
        [System.Serializable]
        public class RankSlot
        {
            public int rank;
            public TMP_Text rankText;
            public TMP_Text playerText;
            public TMP_Text scoreText;
            public GameObject highlight;
        }

        [Header("Dialog")]
        [SerializeField] private GameObject dialogContainer;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text winnerText;

        [Header("Rankings")]
        [SerializeField] private RankSlot[] rankSlots = new RankSlot[4];

        [Header("Buttons")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonLabel;
        [SerializeField] private Button restartButton;
        [SerializeField] private TMP_Text restartButtonLabel;

        [Header("Scene Return")]
        [SerializeField] private SceneReference returnScene;
        [SerializeField] private string returnSceneFallback = "Lobby";
        [SerializeField] private bool autoReturnToScene = true;

        public event Action OnConfirmClicked;
        public event Action OnRestartClicked;

        private void Awake()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
            if (restartButton != null) restartButton.onClick.AddListener(HandleRestartClicked);
        }

        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            if (restartButton != null) restartButton.onClick.RemoveListener(HandleRestartClicked);
        }

        public void Show(IReadOnlyList<Player> players, int winnerIndex)
        {
            if (players == null) return;

            if (titleText != null) titleText.text = "게임 종료";

            if (winnerText != null && winnerIndex >= 0 && winnerIndex < players.Count)
            {
                var winner = players[winnerIndex];
                string marker = winner.IsHuman ? " (나)" : "";
                winnerText.text = $"승자: P{winner.Index}{marker}";
            }

            // 점수 내림차순 정렬
            var sorted = new List<Player>(players);
            sorted.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));

            for (int i = 0; i < rankSlots.Length; i++)
            {
                var slot = rankSlots[i];
                if (slot == null) continue;

                if (i < sorted.Count)
                {
                    var p = sorted[i];
                    int rank = i + 1;

                    if (slot.rankText != null) slot.rankText.text = $"{rank}등";
                    if (slot.playerText != null)
                    {
                        string marker = p.IsHuman ? " (나)" : "";
                        slot.playerText.text = $"P{p.Index}{marker}";
                    }
                    if (slot.scoreText != null) slot.scoreText.text = $"{p.TotalScore}점";
                    if (slot.highlight != null) slot.highlight.SetActive(rank == 1);
                }
                else
                {
                    if (slot.rankText != null) slot.rankText.text = "";
                    if (slot.playerText != null) slot.playerText.text = "";
                    if (slot.scoreText != null) slot.scoreText.text = "";
                    if (slot.highlight != null) slot.highlight.SetActive(false);
                }
            }

            if (confirmButtonLabel != null && string.IsNullOrEmpty(confirmButtonLabel.text))
                confirmButtonLabel.text = "확인";
            if (restartButtonLabel != null && string.IsNullOrEmpty(restartButtonLabel.text))
                restartButtonLabel.text = "다시 시작";

            if (confirmButton != null) confirmButton.interactable = true;
            if (restartButton != null) restartButton.interactable = true;

            if (dialogContainer != null) dialogContainer.SetActive(true);
        }

        public void Hide()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
        }

        private void HandleConfirmClicked()
        {
            Debug.Log("[FinalResultPanel] 확인 클릭 (로비 복귀)");
            if (confirmButton != null) confirmButton.interactable = false;
            if (restartButton != null) restartButton.interactable = false;
            Hide();
            OnConfirmClicked?.Invoke();

            if (autoReturnToScene)
            {
                string scene = (returnScene != null && returnScene.IsValid)
                    ? returnScene.SceneName
                    : returnSceneFallback;
                Debug.Log($"[FinalResultPanel] → {scene}");
                SceneManager.LoadScene(scene);
            }
        }

        private void HandleRestartClicked()
        {
            Debug.Log("[FinalResultPanel] 다시 시작 클릭");
            if (confirmButton != null) confirmButton.interactable = false;
            if (restartButton != null) restartButton.interactable = false;
            Hide();
            OnRestartClicked?.Invoke();
        }
    }
}
