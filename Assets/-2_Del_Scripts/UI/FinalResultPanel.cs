using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 게임 최종 결과창 v4 — 확인 버튼 + 다음 대국 버튼 분리.
    /// 
    /// 두 가지 버튼:
    ///   - 확인 (Confirm): 로비 씬으로 복귀
    ///   - 다음 대국 (Next Match): 같은 씬에서 새 게임 시작
    /// </summary>
    public class FinalResultPanel : MonoBehaviour
    {
        [System.Serializable]
        public class RankEntry
        {
            public int rank;
            public TMP_Text rankText;
            public TMP_Text playerText;
            public TMP_Text seatText;
            public TMP_Text pointsText;
            public GameObject highlight;
        }

        [Header("Dialog Container (자식, 처음 비활성)")]
        [SerializeField] private GameObject dialogContainer;

        [Header("Title / Info")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text infoText;

        [Header("Rank Entries (4슬롯)")]
        [SerializeField] private RankEntry[] entries = new RankEntry[4];

        [Header("Confirm Button (로비로 복귀)")]
        [SerializeField] private Button confirmButton;
        [SerializeField] private TMP_Text confirmButtonLabel;

        [Header("Next Match Button (대국 재시작)")]
        [SerializeField] private Button nextMatchButton;
        [SerializeField] private TMP_Text nextMatchButtonLabel;

        [Header("Return Scene (확인 버튼 클릭 시)")]
        [SerializeField] private SceneReference returnScene;
        [SerializeField] private string returnSceneFallback = "Lobby";

        [Tooltip("확인 버튼 클릭 시 자동으로 returnScene으로 이동")]
        [SerializeField] private bool autoReturnToScene = true;

        [Header("Next Match (Game Manager)")]
        [Tooltip("다음 대국 버튼 클릭 시 호출할 GameManager")]
        [SerializeField] private MahjongGameManager gameManager;

        public event Action OnConfirmClicked;
        public event Action OnNextMatchClicked;
        public event Action OnRestartClicked; // 구버전 호환 (= OnConfirmClicked)

        private void Awake()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            if (confirmButton != null) confirmButton.onClick.AddListener(HandleConfirmClicked);
            if (nextMatchButton != null) nextMatchButton.onClick.AddListener(HandleNextMatchClicked);

            if (gameManager == null) gameManager = FindObjectOfType<MahjongGameManager>();
        }

        private void OnDestroy()
        {
            if (confirmButton != null) confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            if (nextMatchButton != null) nextMatchButton.onClick.RemoveListener(HandleNextMatchClicked);
        }

        public void Show(FinalResult result)
        {
            if (result == null) return;

            if (titleText != null) titleText.text = "게임 종료";

            if (infoText != null)
            {
                string handsLabel = $"{result.HandsPlayed}국 진행";
                if (result.WasForceEnded) handsLabel += " (강제 종료)";
                infoText.text = handsLabel;
            }

            if (confirmButtonLabel != null && string.IsNullOrEmpty(confirmButtonLabel.text))
                confirmButtonLabel.text = "확인";
            if (nextMatchButtonLabel != null && string.IsNullOrEmpty(nextMatchButtonLabel.text))
                nextMatchButtonLabel.text = "다음 대국";

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                var rankInfo = FindByRank(result, entry.rank);
                if (rankInfo == null)
                {
                    if (entry.rankText != null) entry.rankText.text = "";
                    if (entry.playerText != null) entry.playerText.text = "";
                    if (entry.seatText != null) entry.seatText.text = "";
                    if (entry.pointsText != null) entry.pointsText.text = "";
                    if (entry.highlight != null) entry.highlight.SetActive(false);
                    continue;
                }
                UpdateEntry(entry, rankInfo);
            }

            // 버튼 활성화 (중복 클릭 방지용 비활성 해제)
            if (confirmButton != null) confirmButton.interactable = true;
            if (nextMatchButton != null) nextMatchButton.interactable = true;

            if (dialogContainer != null) dialogContainer.SetActive(true);
        }

        public void Hide()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
        }

        private FinalResult.PlayerEntry FindByRank(FinalResult result, int rank)
        {
            foreach (var entry in result.Rankings)
                if (entry.Rank == rank) return entry;
            return null;
        }

        private void UpdateEntry(RankEntry slot, FinalResult.PlayerEntry data)
        {
            if (slot.rankText != null)
                slot.rankText.text = $"{data.Rank}등";

            if (slot.playerText != null)
            {
                string typeMarker = data.Type == PlayerType.Human ? "(나)" : "";
                slot.playerText.text = $"P{data.PlayerIndex} {typeMarker}";
            }

            if (slot.seatText != null)
                slot.seatText.text = SeatToKanji(data.FinalSeat);

            if (slot.pointsText != null)
                slot.pointsText.text = $"{data.FinalPoints:N0}";

            if (slot.highlight != null)
                slot.highlight.SetActive(data.Rank == 1);
        }

        private static string SeatToKanji(SeatWind seat)
        {
            switch (seat)
            {
                case SeatWind.East: return "東";
                case SeatWind.South: return "南";
                case SeatWind.West: return "西";
                case SeatWind.North: return "北";
                default: return "?";
            }
        }

        // === 버튼 핸들러 ===

        private void HandleConfirmClicked()
        {
            Debug.Log("[FinalResultPanel] 확인 버튼 클릭 (로비로 복귀)");

            // 중복 클릭 방지
            if (confirmButton != null) confirmButton.interactable = false;
            if (nextMatchButton != null) nextMatchButton.interactable = false;

            Hide();

            OnConfirmClicked?.Invoke();
            OnRestartClicked?.Invoke();

            if (autoReturnToScene)
            {
                string sceneName = (returnScene != null && returnScene.IsValid)
                    ? returnScene.SceneName
                    : returnSceneFallback;
                Debug.Log($"[FinalResultPanel] → {sceneName}");
                SceneManager.LoadScene(sceneName);
            }
        }

        private void HandleNextMatchClicked()
        {
            Debug.Log("[FinalResultPanel] 다음 대국 버튼 클릭 (게임 재시작)");

            // 중복 클릭 방지
            if (confirmButton != null) confirmButton.interactable = false;
            if (nextMatchButton != null) nextMatchButton.interactable = false;

            Hide();

            OnNextMatchClicked?.Invoke();

            // GameManager에 재시작 요청
            if (gameManager != null)
            {
                gameManager.StartGame();
            }
            else
            {
                Debug.LogWarning("[FinalResultPanel] GameManager 미연결 — 재시작 못 함");
            }
        }
    }
}