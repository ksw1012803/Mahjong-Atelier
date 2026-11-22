using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 4명 점수판. 중앙 영역 또는 각 플레이어 영역에 배치 가능.
    /// 
    /// 두 가지 사용법:
    ///   1) 중앙 4분할 점수판 (이 컴포넌트 1개)
    ///   2) 각 플레이어 영역에 작은 점수 패널 (각 플레이어용 ScoreEntry를 따로 컴포넌트화)
    /// 
    /// 본 구현은 4명을 한 번에 표시하는 중앙 방식.
    /// </summary>
    public class ScoreBoardView : MonoBehaviour
    {
        [System.Serializable]
        public class PlayerScoreSlot
        {
            [Tooltip("이 슬롯이 보여줄 플레이어 인덱스 (0~3)")]
            public int playerIndex;

            [Tooltip("좌석풍 표시 (東/南/西/北)")]
            public TMP_Text seatWindText;

            [Tooltip("점수 표시")]
            public TMP_Text pointsText;

            [Tooltip("이 플레이어가 친(東家)일 때 강조용 (선택, 비워둬도 됨)")]
            public GameObject dealerIndicator;

            [Tooltip("리치 마크 (선택)")]
            public GameObject riichiIndicator;
        }

        [Header("Player Slots (4개 채우기)")]
        [SerializeField] private PlayerScoreSlot[] slots = new PlayerScoreSlot[4];

        [Header("Center Display (선택)")]
        [Tooltip("장풍 표시 텍스트 (예: 東場)")]
        [SerializeField] private TMP_Text roundWindText;

        [Tooltip("공탁금(리치봉) 합계 표시")]
        [SerializeField] private TMP_Text depositsText;

        [Tooltip("남은 패산 표시")]
        [SerializeField] private TMP_Text wallRemainingText;

        [Header("Game Manager")]
        [SerializeField] private MahjongGameManager gameManager;

        private void Awake()
        {
            if (gameManager == null)
                gameManager = FindObjectOfType<MahjongGameManager>();
        }

        private void OnEnable()
        {
            GameEvents.OnAgari += HandleAgari;
            GameEvents.OnRiichiDeclared += HandleRiichiDeclared;
            GameEvents.OnWallCountChanged += HandleWallCountChanged;
            GameEvents.OnDealCompleted += HandleDealCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OnAgari -= HandleAgari;
            GameEvents.OnRiichiDeclared -= HandleRiichiDeclared;
            GameEvents.OnWallCountChanged -= HandleWallCountChanged;
            GameEvents.OnDealCompleted -= HandleDealCompleted;
        }

        private void Start()
        {
            UpdateAll();
        }

        // ====================================================
        // 이벤트 핸들러
        // ====================================================

        private void HandleAgari(int winnerIdx, AgariForm form) => UpdateAll();
        private void HandleRiichiDeclared(int playerIdx) => UpdateAll();
        private void HandleDealCompleted() => UpdateAll();
        private void HandleWallCountChanged(int remaining)
        {
            if (wallRemainingText != null)
                wallRemainingText.text = $"패산 {remaining}";
        }

        // ====================================================
        // 디스플레이
        // ====================================================

        public void UpdateAll()
        {
            if (gameManager == null) return;

            // 각 슬롯 갱신
            foreach (var slot in slots)
            {
                if (slot == null) continue;
                if (slot.playerIndex >= gameManager.Players.Count) continue;
                UpdateSlot(slot);
            }

            // 중앙 정보
            UpdateCenterInfo();
        }

        private void UpdateSlot(PlayerScoreSlot slot)
        {
            var player = gameManager.Players[slot.playerIndex];
            var score = gameManager.Scores[slot.playerIndex];

            if (slot.seatWindText != null)
                slot.seatWindText.text = SeatWindToKanji(player.Seat);

            if (slot.pointsText != null)
                slot.pointsText.text = $"{score.Points:N0}";

            if (slot.dealerIndicator != null)
                slot.dealerIndicator.SetActive(player.IsDealer);

            if (slot.riichiIndicator != null)
                slot.riichiIndicator.SetActive(player.IsRiichi);
        }

        private void UpdateCenterInfo()
        {
            if (roundWindText != null && gameManager.Players.Count > 0)
            {
                // 장풍은 게임 매니저에서 가져오기 — 일단 기본 동
                // 추후 GameManager에 RoundWind 공개 프로퍼티 추가 가능
                roundWindText.text = "東";
            }

            if (depositsText != null)
            {
                int deposits = gameManager.RiichiDeposits;
                depositsText.gameObject.SetActive(deposits > 0);
                depositsText.text = $"공탁 {deposits}";
            }

            if (wallRemainingText != null && gameManager.Wall != null)
            {
                wallRemainingText.text = $"패산 {gameManager.Wall.RemainingDrawable}";
            }
        }

        private static string SeatWindToKanji(SeatWind seat)
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
    }
}
