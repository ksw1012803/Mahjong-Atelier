using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 리치 버튼 + 리치 선언 모드 컨트롤러.
    /// 
    /// 동작:
    ///   1) 평소: 버튼 비활성 (회색)
    ///   2) 리치 가능 상태가 되면 → 버튼 활성화
    ///   3) 버튼 클릭 → "리치 선언 모드" 진입
    ///      - 버릴 수 있는 패만 강조 표시 (다른 패는 어둡게)
    ///      - 안내 텍스트 표시
    ///   4) 강조된 패 클릭 → RequestRiichi
    ///   5) 다시 버튼 클릭 → 리치 선언 모드 취소
    /// 
    /// Inspector에 연결할 것:
    ///   - riichiButton: Button
    ///   - buttonLabel: 버튼 위 텍스트 (선택)
    ///   - hintText: "리치할 패를 선택하세요" 같은 안내
    ///   - gameManager: MahjongGameManager
    ///   - handUI: HandUIManager (패 강조 제어용)
    /// </summary>
    public class RiichiButton : MonoBehaviour
    {
        [Header("Button")]
        [SerializeField] private Button riichiButton;
        [SerializeField] private TMP_Text buttonLabel;

        [Header("Hint")]
        [SerializeField] private GameObject hintPanel;
        [SerializeField] private TMP_Text hintText;

        [Header("References")]
        [SerializeField] private MahjongGameManager gameManager;
        [SerializeField] private HandUIManager handUI;

        // 현재 리치 가능 상태인 패 목록 (서버에서 받음)
        private List<TileData> _currentAvailableDiscards = new List<TileData>();

        // 리치 선언 모드 (버튼 누름) 여부
        private bool _inRiichiSelectionMode = false;

        private void OnEnable()
        {
            GameEvents.OnRiichiAvailabilityChanged += HandleRiichiAvailability;
            GameEvents.OnRiichiDeclared += HandleRiichiDeclared;
            GameEvents.OnTurnStarted += HandleTurnStarted;

            if (riichiButton != null)
                riichiButton.onClick.AddListener(HandleButtonClicked);
        }

        private void OnDisable()
        {
            GameEvents.OnRiichiAvailabilityChanged -= HandleRiichiAvailability;
            GameEvents.OnRiichiDeclared -= HandleRiichiDeclared;
            GameEvents.OnTurnStarted -= HandleTurnStarted;

            if (riichiButton != null)
                riichiButton.onClick.RemoveListener(HandleButtonClicked);
        }

        private void Start()
        {
            SetButtonEnabled(false);
            if (hintPanel != null) hintPanel.SetActive(false);
        }

        // ====================================================
        // 이벤트 핸들러
        // ====================================================

        private void HandleRiichiAvailability(int playerIndex, RiichiAnalysis analysis)
        {
            // 1인용에선 P0만, 4인용에선 사용자 플레이어만 봐야 함
            if (playerIndex != 0) return;

            _currentAvailableDiscards = analysis.AvailableDiscards;
            SetButtonEnabled(analysis.CanDeclareRiichi);

            // 리치 가능하다는 표시만 (선택 모드는 별개)
            if (!analysis.CanDeclareRiichi && _inRiichiSelectionMode)
            {
                ExitSelectionMode();
            }
        }

        private void HandleRiichiDeclared(int playerIndex)
        {
            if (playerIndex != 0) return;
            // 리치 선언 후 버튼은 영구 비활성
            SetButtonEnabled(false);
            if (buttonLabel != null) buttonLabel.text = "리치 중";
            ExitSelectionMode();
        }

        private void HandleTurnStarted(int playerIndex)
        {
            // 새 턴 시작 시 기본 상태로 리셋
            ExitSelectionMode();
        }

        // ====================================================
        // 버튼/상호작용
        // ====================================================

        private void HandleButtonClicked()
        {
            if (_inRiichiSelectionMode)
                ExitSelectionMode();
            else
                EnterSelectionMode();
        }

        private void EnterSelectionMode()
        {
            _inRiichiSelectionMode = true;
            ShowHint("리치 선언패를 선택하세요\n(밝게 표시된 패만 가능)");

            // HandUI에 강조 모드 적용 — 가능한 패만 밝게
            if (handUI != null)
                handUI.SetTileHighlightFilter(_currentAvailableDiscards);
        }

        private void ExitSelectionMode()
        {
            if (!_inRiichiSelectionMode) return;
            _inRiichiSelectionMode = false;
            HideHint();
            if (handUI != null)
                handUI.ClearTileHighlightFilter();
        }

        /// <summary>
        /// HandUI에서 타일이 클릭될 때 호출. 리치 모드면 리치 선언, 아니면 일반 버림.
        /// HandUIManager.OnTileClicked가 이 메서드를 거치도록 해야 함.
        /// </summary>
        public bool TryHandleTileClick(TileData tile)
        {
            if (!_inRiichiSelectionMode) return false;

            // 가능한 패인지 확인
            bool isValid = false;
            foreach (var t in _currentAvailableDiscards)
            {
                if (t == tile) { isValid = true; break; }
            }
            if (!isValid)
            {
                Debug.Log("[Riichi] 이 패는 텐파이가 깨집니다");
                return true; // 처리는 한 것 (다른 곳으로 안 넘김)
            }

            // 리치 선언 요청
            if (gameManager != null)
            {
                bool ok = gameManager.RequestRiichi(tile);
                if (ok) ExitSelectionMode();
            }
            return true;
        }

        // ====================================================
        // UI 헬퍼
        // ====================================================

        private void SetButtonEnabled(bool enabled)
        {
            if (riichiButton == null) return;
            riichiButton.interactable = enabled;
            if (buttonLabel != null && !_inRiichiSelectionMode)
                buttonLabel.text = "리치";
        }

        private void ShowHint(string text)
        {
            if (hintPanel != null) hintPanel.SetActive(true);
            if (hintText != null) hintText.text = text;
        }

        private void HideHint()
        {
            if (hintPanel != null) hintPanel.SetActive(false);
        }

        public bool IsInRiichiSelectionMode => _inRiichiSelectionMode;
    }
}
