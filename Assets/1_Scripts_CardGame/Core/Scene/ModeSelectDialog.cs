using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 모드 선택 다이얼로그 v5 — 싱글플레이 전용 (온라인은 OnlineLobbyPanel).
    ///
    /// v5 변경:
    ///   - OnModeSelected에서 SceneContextKeys.OnlineSession = false 명시 → CardGameManager Local 모드.
    ///   - SelectedModeIndex 저장.
    /// v4.3:
    ///   - Scene 전환을 SceneLoader.Load(targetScene, useLoadingScreen: true)로 복원.
    ///     SceneLoader가 LoadScene을 거쳐 CardGameScene으로 전환.
    ///   - CanvasGroup 패턴으로 첫 클릭 씹힘 제거 (v4의 핵심 유지).
    ///   - 패널 pop + 카드 stagger 등장 애니메이션 (v4.2의 핵심 유지).
    ///   - SceneContext.Set("selectedMode", mode) 유지 — CardGame 측에서 참조.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ModeSelectDialog : MonoBehaviour
    {
        [Header("Mode Data")]
        [SerializeField] private ModeDefinition[] modeDefinitions;
        [SerializeField] private ModeCardButton[] modeCards;

        [Header("Scene Transition")]
        [Tooltip("최종 목적지 씬. SceneLoader가 LoadScene을 거쳐 이쪽으로 전환.")]
        [SerializeField] private GameScene targetScene = GameScene.CardGameScene;

        [Header("Context Keys")]
        [SerializeField] private string selectedModeKey = "selectedMode";

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Header("Fade")]
        [SerializeField] private float fadeDuration = 0.2f;

        [Header("Panel Pop Animation")]
        [Tooltip("다이얼로그 패널 RectTransform (scale pop 대상). 비워두면 skip.")]
        [SerializeField] private RectTransform dialogPanel;
        [SerializeField, Range(0.5f, 1f)] private float panelStartScale = 0.85f;
        [SerializeField] private float panelPopDuration = 0.35f;

        [Header("Card Entrance Animation")]
        [SerializeField] private Vector2 cardStartOffset = new Vector2(0f, -60f);
        [SerializeField, Range(0.5f, 1f)] private float cardStartScale = 0.7f;
        [SerializeField] private float cardStagger = 0.07f;
        [SerializeField] private float cardAnimDuration = 0.4f;
        [SerializeField] private Ease cardEase = Ease.OutBack;

        private CanvasGroup _canvasGroup;
        private bool _isShowing = false;
        private ModeDefinition _selectedMode;
        private bool _initialized = false;

        private Vector2[] _restPositions;

        private void Awake()
        {
            EnsureCanvasGroup();
            HideImmediate();
            BindModeCards();
            CacheRestPositions();

            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Hide);
            }

            _initialized = true;
            Debug.Log("[ModeSelectDialog] Awake 완료 — bind + rest 캐시");
        }

        private void EnsureCanvasGroup()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void BindModeCards()
        {
            if (modeCards == null || modeDefinitions == null)
            {
                Debug.LogWarning("[ModeSelectDialog] modeCards 또는 modeDefinitions가 null");
                return;
            }

            int count = Mathf.Min(modeCards.Length, modeDefinitions.Length);
            if (modeCards.Length != modeDefinitions.Length)
            {
                Debug.LogWarning($"[ModeSelectDialog] modeCards({modeCards.Length}) ≠ modeDefinitions({modeDefinitions.Length}) — min({count}) 만큼만 bind");
            }

            for (int i = 0; i < count; i++)
            {
                var card = modeCards[i];
                var mode = modeDefinitions[i];
                if (card == null || mode == null) continue;

                card.Bind(mode, OnModeSelected);
            }
        }

        private void CacheRestPositions()
        {
            if (modeCards == null) return;
            _restPositions = new Vector2[modeCards.Length];
            for (int i = 0; i < modeCards.Length; i++)
            {
                if (modeCards[i] == null) continue;
                var rt = modeCards[i].transform as RectTransform;
                if (rt != null) _restPositions[i] = rt.anchoredPosition;
            }
        }

        private void OnModeSelected(ModeDefinition mode)
        {
            if (mode == null) return;

            _selectedMode = mode;
            Debug.Log($"[ModeSelectDialog] 선택: {mode.displayName} → {targetScene}");

            // 선택된 모드만 SceneContext에 저장 (CardGame 측 참조용)
            SceneContext.Set(selectedModeKey, mode);
            SceneContext.Set(SceneContextKeys.SelectedModeIndex, System.Array.IndexOf(modeDefinitions, mode));

            // ★ v5: 싱글플레이 경로 명시 — CardGameManager가 Local 모드로 자동 시작
            SceneContext.Set(SceneContextKeys.OnlineSession, false);

            Hide();

            // ★ v4.3: 원래 패턴 복원 — SceneLoader가 LoadScene 거쳐 target으로 전환
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.Load(targetScene, useLoadingScreen: true);
            }
            else
            {
                Debug.LogWarning("[ModeSelectDialog] SceneLoader.Instance NULL. SceneManager 폴백");
                UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene.ToString());
            }
        }

        // ========== Public API ==========

        public void Show()
        {
            if (!_initialized)
            {
                EnsureCanvasGroup();
                BindModeCards();
                CacheRestPositions();
                _initialized = true;
            }

            if (_isShowing) return;
            _isShowing = true;

            _canvasGroup.DOKill();
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            _canvasGroup.DOFade(1f, fadeDuration).SetLink(gameObject);

            AnimatePanelPop();
            AnimateCardsIn();
        }

        public void Hide()
        {
            if (!_isShowing) return;
            _isShowing = false;

            _canvasGroup.DOKill();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _canvasGroup.DOFade(0f, fadeDuration).SetLink(gameObject);

            KillCardAnimations();
        }

        private void HideImmediate()
        {
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _isShowing = false;
        }

        // ========== Entrance Animations ==========

        private void AnimatePanelPop()
        {
            if (dialogPanel == null) return;

            dialogPanel.DOKill();
            dialogPanel.localScale = Vector3.one * panelStartScale;
            dialogPanel.DOScale(Vector3.one, panelPopDuration)
                       .SetEase(Ease.OutBack)
                       .SetLink(dialogPanel.gameObject);
        }

        private void AnimateCardsIn()
        {
            if (modeCards == null || _restPositions == null) return;

            for (int i = 0; i < modeCards.Length; i++)
            {
                var card = modeCards[i];
                if (card == null) continue;

                var rt = card.transform as RectTransform;
                if (rt == null) continue;

                Vector2 restPos = _restPositions[i];
                Vector2 startPos = restPos + cardStartOffset;

                rt.DOKill();
                rt.anchoredPosition = startPos;
                rt.localScale = Vector3.one * cardStartScale;

                var cg = card.GetComponent<CanvasGroup>();
                if (cg == null) cg = card.gameObject.AddComponent<CanvasGroup>();
                cg.DOKill();
                cg.alpha = 0f;

                float delay = i * cardStagger;
                var go = card.gameObject;

                rt.DOAnchorPos(restPos, cardAnimDuration)
                  .SetEase(cardEase)
                  .SetDelay(delay)
                  .SetLink(go);
                rt.DOScale(Vector3.one, cardAnimDuration)
                  .SetEase(cardEase)
                  .SetDelay(delay)
                  .SetLink(go);
                cg.DOFade(1f, cardAnimDuration * 0.7f)
                  .SetDelay(delay)
                  .SetLink(go);
            }
        }

        private void KillCardAnimations()
        {
            if (modeCards == null) return;
            foreach (var card in modeCards)
            {
                if (card == null) continue;
                card.transform.DOKill();
                var cg = card.GetComponent<CanvasGroup>();
                if (cg != null) cg.DOKill();
            }
            if (dialogPanel != null) dialogPanel.DOKill();
        }

        public bool IsShowing => _isShowing;
    }
}
