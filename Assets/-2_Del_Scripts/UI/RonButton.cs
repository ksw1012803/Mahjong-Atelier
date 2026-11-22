using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 론 버튼 v2 — 변수 캡처 버그 수정.
    /// 
    /// 수정점:
    ///   - HandleRonClicked에서 HideButtons() 전에 변수 캡처
    ///   - 디버그 로그 추가
    /// </summary>
    public class RonButton : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private GameObject buttonContainer;
        [SerializeField] private Button ronButton;
        [SerializeField] private Button passButton;

        [Header("Optional UI")]
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text infoText;

        [Header("Settings")]
        [SerializeField] private float decisionTimeoutSeconds = 5f;

        [Header("References")]
        [SerializeField] private MahjongGameManager gameManager;

        private TileData _currentDiscardedTile;
        private int _currentDiscarderIndex = -1;
        private float _remainingTime;
        private bool _isWaitingForDecision;

        private void Awake()
        {
            if (buttonContainer != null) buttonContainer.SetActive(false);
            if (ronButton != null) ronButton.onClick.AddListener(HandleRonClicked);
            if (passButton != null) passButton.onClick.AddListener(HandlePassClicked);
            if (gameManager == null) gameManager = FindObjectOfType<MahjongGameManager>();
        }

        private void OnDestroy()
        {
            if (ronButton != null) ronButton.onClick.RemoveListener(HandleRonClicked);
            if (passButton != null) passButton.onClick.RemoveListener(HandlePassClicked);
        }

        private void OnEnable()
        {
            GameEvents.OnRonOpportunity += HandleRonOpportunity;
            GameEvents.OnRonOpportunityClosed += HandleRonOpportunityClosed;
        }

        private void OnDisable()
        {
            GameEvents.OnRonOpportunity -= HandleRonOpportunity;
            GameEvents.OnRonOpportunityClosed -= HandleRonOpportunityClosed;
        }

        private void Update()
        {
            if (!_isWaitingForDecision || decisionTimeoutSeconds <= 0f) return;

            _remainingTime -= Time.deltaTime;
            if (timerText != null)
                timerText.text = $"{Mathf.Max(0, Mathf.CeilToInt(_remainingTime))}";

            if (_remainingTime <= 0f)
            {
                HandlePassClicked();
            }
        }

        private void HandleRonOpportunity(TileData discardedTile, int discarderIndex,
            System.Collections.Generic.List<RonCandidate> candidates)
        {
            int userIndex = gameManager != null ? gameManager.UserPlayerIndex : 0;
            bool userCanRon = false;
            foreach (var c in candidates)
            {
                if (c.PlayerIndex == userIndex) { userCanRon = true; break; }
            }

            if (!userCanRon) return;

            _currentDiscardedTile = discardedTile;
            _currentDiscarderIndex = discarderIndex;
            _isWaitingForDecision = true;
            _remainingTime = decisionTimeoutSeconds;

            Debug.Log($"[RonButton] 론 기회 수신: tile={_currentDiscardedTile}, discarder=P{_currentDiscarderIndex}");

            ShowButtons(discardedTile);
        }

        private void HandleRonOpportunityClosed()
        {
            HideButtons();
        }

        // === 핵심 수정: 변수 캡처 ===

        private void HandleRonClicked()
        {
            if (!_isWaitingForDecision) return;

            // ★ HideButtons() 전에 변수 캡처
            var capturedTile = _currentDiscardedTile;
            var capturedDiscarder = _currentDiscarderIndex;

            Debug.Log($"[RonButton] Ron 클릭됨. 캡처: tile={capturedTile}, discarder=P{capturedDiscarder}");

            _isWaitingForDecision = false;
            HideButtons();

            if (gameManager != null)
            {
                gameManager.RequestRon(gameManager.UserPlayerIndex, capturedTile, capturedDiscarder);
            }
        }

        private void HandlePassClicked()
        {
            if (!_isWaitingForDecision) return;
            _isWaitingForDecision = false;
            HideButtons();

            if (gameManager != null)
            {
                gameManager.PassRonOpportunity(gameManager.UserPlayerIndex);
            }
        }

        private void ShowButtons(TileData tile)
        {
            if (buttonContainer != null) buttonContainer.SetActive(true);
            if (infoText != null) infoText.text = $"{tile} 으로 론 가능!";
            if (timerText != null && decisionTimeoutSeconds > 0f)
                timerText.text = $"{Mathf.CeilToInt(decisionTimeoutSeconds)}";
        }

        private void HideButtons()
        {
            if (buttonContainer != null) buttonContainer.SetActive(false);
            _currentDiscardedTile = null;
            _currentDiscarderIndex = -1;
        }
    }
}
