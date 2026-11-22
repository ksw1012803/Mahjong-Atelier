using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 게임 정보 헤더.
    /// 
    /// 표시:
    ///   - 현재 국 (예: 東1局, 南3局)
    ///   - 본장 (本場)
    ///   - 강제 종료 버튼 (테스트용)
    /// 
    /// 게임 매니저에서 GameMatchState 정보를 가져와 갱신.
    /// </summary>
    public class GameInfoHeader : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private TMP_Text handLabelText;  // "東1局" 등
        [SerializeField] private TMP_Text honbaText;       // "0本場"

        [Header("Force End Button (테스트용)")]
        [SerializeField] private Button forceEndButton;
        [SerializeField] private GameObject confirmDialog; // 확인 다이얼로그 (선택)
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        [Header("References")]
        [SerializeField] private MahjongGameManager gameManager;

        private void Awake()
        {
            if (forceEndButton != null) forceEndButton.onClick.AddListener(HandleForceEndClicked);
            if (confirmYesButton != null) confirmYesButton.onClick.AddListener(HandleConfirmYes);
            if (confirmNoButton != null) confirmNoButton.onClick.AddListener(HandleConfirmNo);
            if (confirmDialog != null) confirmDialog.SetActive(false);

            if (gameManager == null) gameManager = FindObjectOfType<MahjongGameManager>();
        }

        private void OnDestroy()
        {
            if (forceEndButton != null) forceEndButton.onClick.RemoveListener(HandleForceEndClicked);
            if (confirmYesButton != null) confirmYesButton.onClick.RemoveListener(HandleConfirmYes);
            if (confirmNoButton != null) confirmNoButton.onClick.RemoveListener(HandleConfirmNo);
        }

        private void OnEnable()
        {
            GameEvents.OnDealStarted += UpdateDisplay;
            GameEvents.OnAgari += HandleAgari;
        }

        private void OnDisable()
        {
            GameEvents.OnDealStarted -= UpdateDisplay;
            GameEvents.OnAgari -= HandleAgari;
        }

        private void Start()
        {
            UpdateDisplay();
        }

        private void HandleAgari(int winnerIdx, AgariForm form) => UpdateDisplay();

        public void UpdateDisplay()
        {
            if (gameManager == null) return;

            // 현재 국 라벨
            if (handLabelText != null)
            {
                handLabelText.text = gameManager.GetCurrentHandLabel();
            }

            // 본장
            if (honbaText != null)
            {
                int honba = gameManager.HonbaCount;
                honbaText.text = honba > 0 ? $"{honba}本場" : "";
            }
        }

        // === 강제 종료 ===

        private void HandleForceEndClicked()
        {
            if (confirmDialog != null)
            {
                confirmDialog.SetActive(true);
            }
            else
            {
                // 확인 다이얼로그 없으면 즉시 종료
                if (gameManager != null) gameManager.ForceEndGame();
            }
        }

        private void HandleConfirmYes()
        {
            if (confirmDialog != null) confirmDialog.SetActive(false);
            if (gameManager != null) gameManager.ForceEndGame();
        }

        private void HandleConfirmNo()
        {
            if (confirmDialog != null) confirmDialog.SetActive(false);
        }
    }
}
