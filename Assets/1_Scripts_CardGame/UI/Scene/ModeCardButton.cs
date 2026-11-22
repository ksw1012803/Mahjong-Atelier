using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 모드 선택 카드 v2 — Null 안전 처리.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ModeCardButton : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Display")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image backgroundImage;

        [Header("Hover")]
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float hoverLift = 20f;
        [SerializeField] private float hoverDuration = 0.15f;

        private RectTransform _rect;
        private ModeDefinition _mode;
        private Action<ModeDefinition> _onClick;
        private Vector2 _restPosition;
        private bool _hovered;
        private bool _restPositionCaptured;

        private void Awake()
        {
            EnsureRect();
        }

        private void EnsureRect()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
        }

        public void Bind(ModeDefinition mode, Action<ModeDefinition> onClick)
        {
            EnsureRect();  // ★ Awake 순서 무관하게 안전

            _mode = mode;
            _onClick = onClick;

            if (_rect != null && !_restPositionCaptured)
            {
                _restPosition = _rect.anchoredPosition;
                _restPositionCaptured = true;
            }

            if (mode == null)
            {
                Debug.LogWarning("[ModeCardButton] Bind: mode가 null");
                return;
            }

            if (titleText != null) titleText.text = mode.displayName;
            if (descriptionText != null) descriptionText.text = mode.description;
            if (iconImage != null && mode.icon != null) iconImage.sprite = mode.icon;
            if (backgroundImage != null) backgroundImage.color = mode.themeColor;
        }

        public Vector2 GetRestPosition()
        {
            EnsureRect();
            if (!_restPositionCaptured && _rect != null)
            {
                _restPosition = _rect.anchoredPosition;
                _restPositionCaptured = true;
            }
            return _restPosition;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_hovered || _rect == null) return;
            _hovered = true;
            _rect.DOKill();
            _rect.DOScale(hoverScale, hoverDuration).SetEase(Ease.OutQuad);
            _rect.DOAnchorPos(_restPosition + new Vector2(0f, hoverLift), hoverDuration).SetEase(Ease.OutQuad);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_hovered || _rect == null) return;
            _hovered = false;
            _rect.DOKill();
            _rect.DOScale(1f, hoverDuration).SetEase(Ease.OutQuad);
            _rect.DOAnchorPos(_restPosition, hoverDuration).SetEase(Ease.OutQuad);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_mode == null || _onClick == null || _rect == null) return;
            _rect.DOKill();
            _rect.DOScale(0.95f, 0.08f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                if (_rect != null) _rect.DOScale(hoverScale, 0.1f);
                _onClick?.Invoke(_mode);
            });
        }

        private void OnDestroy()
        {
            if (_rect != null) _rect.DOKill();
        }
    }
}