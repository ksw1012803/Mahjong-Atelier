using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 후로 버튼 — 치 디버그 강화.
    /// </summary>
    public class CallButton : MonoBehaviour
    {
        [Header("Buttons Container (자식, 처음 비활성)")]
        [SerializeField] private GameObject buttonContainer;

        [Header("Individual Buttons")]
        [SerializeField] private Button chiButton;
        [SerializeField] private Button ponButton;
        [SerializeField] private Button daiminkanButton;
        [SerializeField] private Button passButton;

        [Header("Optional UI")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text infoText;

        [Header("Chi Selection")]
        [SerializeField] private ChiSelectionUI chiSelectionUI;

        [Header("Settings")]
        [SerializeField] private float decisionTimeoutSeconds = 4f;

        [Header("References")]
        [SerializeField] private MahjongGameManager gameManager;

        private CallOpportunity _currentOpportunity;
        private float _remainingTime;
        private bool _isWaitingForDecision;

        private void Awake()
        {
            if (buttonContainer != null) buttonContainer.SetActive(false);
            if (chiButton != null) chiButton.onClick.AddListener(HandleChiClicked);
            if (ponButton != null) ponButton.onClick.AddListener(HandlePonClicked);
            if (daiminkanButton != null) daiminkanButton.onClick.AddListener(HandleDaiminkanClicked);
            if (passButton != null) passButton.onClick.AddListener(HandlePassClicked);
            if (gameManager == null) gameManager = FindObjectOfType<MahjongGameManager>();
        }

        private void OnDestroy()
        {
            if (chiButton != null) chiButton.onClick.RemoveListener(HandleChiClicked);
            if (ponButton != null) ponButton.onClick.RemoveListener(HandlePonClicked);
            if (daiminkanButton != null) daiminkanButton.onClick.RemoveListener(HandleDaiminkanClicked);
            if (passButton != null) passButton.onClick.RemoveListener(HandlePassClicked);
        }

        private void OnEnable()
        {
            GameEvents.OnCallOpportunity += HandleCallOpportunity;
            GameEvents.OnCallOpportunityClosed += HandleCallOpportunityClosed;
        }

        private void OnDisable()
        {
            GameEvents.OnCallOpportunity -= HandleCallOpportunity;
            GameEvents.OnCallOpportunityClosed -= HandleCallOpportunityClosed;
        }

        private void Update()
        {
            if (!_isWaitingForDecision || decisionTimeoutSeconds <= 0f) return;
            // 치 선택 다이얼로그 열려있는 동안엔 타이머 멈춤
            if (chiSelectionUI != null && chiSelectionUI.IsOpen) return;

            _remainingTime -= Time.deltaTime;
            if (timerText != null)
                timerText.text = $"{Mathf.Max(0, Mathf.CeilToInt(_remainingTime))}";

            if (_remainingTime <= 0f)
                HandlePassClicked();
        }

        private void HandleCallOpportunity(CallOpportunity opp)
        {
            if (opp == null || !opp.HasAny) return;

            _currentOpportunity = opp;
            _isWaitingForDecision = true;
            _remainingTime = decisionTimeoutSeconds;

            Debug.Log($"[CallButton] 후로 기회: Chi={opp.CanChi}({opp.ChiOptions?.Count ?? 0}), Pon={opp.CanPon}, Daiminkan={opp.CanDaiminkan}");

            ShowButtons(opp);
        }

        private void HandleCallOpportunityClosed()
        {
            HideButtons();
        }

        // === 치 ===

        private void HandleChiClicked()
        {
            if (!_isWaitingForDecision || _currentOpportunity == null)
            {
                Debug.LogWarning("[CallButton] Chi 클릭 실패: not waiting or no opportunity");
                return;
            }
            if (_currentOpportunity.ChiOptions == null || _currentOpportunity.ChiOptions.Count == 0)
            {
                Debug.LogWarning("[CallButton] Chi 클릭 실패: no options");
                return;
            }

            // 옵션 1개면 즉시
            if (_currentOpportunity.ChiOptions.Count == 1)
            {
                var option = _currentOpportunity.ChiOptions[0];
                Debug.Log("[CallButton] Chi 옵션 1개 — 즉시 선언");
                ClearWaitingState();
                if (gameManager != null) gameManager.RequestChi(option);
                return;
            }

            // 여러 옵션 → 선택 UI
            if (chiSelectionUI != null)
            {
                Debug.Log($"[CallButton] Chi 옵션 {_currentOpportunity.ChiOptions.Count}개 — 선택 UI 표시");
                chiSelectionUI.Show(_currentOpportunity.ChiOptions, OnChiOptionSelected);
                if (buttonContainer != null) buttonContainer.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[CallButton] ChiSelectionUI 미연결 — 첫 옵션 자동 선택");
                var option = _currentOpportunity.ChiOptions[0];
                ClearWaitingState();
                if (gameManager != null) gameManager.RequestChi(option);
            }
        }

        private void OnChiOptionSelected(ChiOption option)
        {
            Debug.Log($"[CallButton] OnChiOptionSelected 호출됨. option={option}");

            if (option == null)
            {
                Debug.Log("[CallButton] 치 취소 — 버튼 컨테이너 복원");
                if (_currentOpportunity != null && buttonContainer != null)
                    buttonContainer.SetActive(true);
                return;
            }

            Debug.Log($"[CallButton] Chi 선언 요청: SequenceStart={option.SequenceStart}");

            // ★ ClearWaitingState 전에 gameManager 호출 캡처
            var gm = gameManager;
            var capturedOption = option;

            ClearWaitingState();

            if (gm != null)
            {
                bool result = gm.RequestChi(capturedOption);
                Debug.Log($"[CallButton] RequestChi 결과: {result}");
            }
            else
            {
                Debug.LogWarning("[CallButton] gameManager가 null!");
            }
        }

        // === 펑 ===

        private void HandlePonClicked()
        {
            if (!_isWaitingForDecision) return;
            ClearWaitingState();
            if (gameManager != null) gameManager.RequestPon();
        }

        private void HandleDaiminkanClicked()
        {
            if (!_isWaitingForDecision) return;
            ClearWaitingState();
            if (gameManager != null) gameManager.RequestDaiminkan();
        }

        private void HandlePassClicked()
        {
            if (!_isWaitingForDecision) return;
            ClearWaitingState();
            if (gameManager != null) gameManager.PassCallOpportunity();
        }

        private void ClearWaitingState()
        {
            _isWaitingForDecision = false;
            _currentOpportunity = null;
            HideButtons();
        }

        // === UI 헬퍼 ===

        private void ShowButtons(CallOpportunity opp)
        {
            if (buttonContainer != null) buttonContainer.SetActive(true);

            if (chiButton != null) chiButton.gameObject.SetActive(opp.CanChi);
            if (ponButton != null) ponButton.gameObject.SetActive(opp.CanPon);
            if (daiminkanButton != null) daiminkanButton.gameObject.SetActive(opp.CanDaiminkan);

            if (infoText != null)
                infoText.text = $"{opp.DiscardedTile} 후로 가능";
            if (timerText != null && decisionTimeoutSeconds > 0f)
                timerText.text = $"{Mathf.CeilToInt(decisionTimeoutSeconds)}";
        }

        private void HideButtons()
        {
            if (buttonContainer != null) buttonContainer.SetActive(false);
            if (chiSelectionUI != null) chiSelectionUI.Hide();
        }
    }
}