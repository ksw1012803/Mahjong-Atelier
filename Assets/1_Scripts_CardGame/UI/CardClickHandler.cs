using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 카드 클릭 + 드래그 핸들러 v9.2 — HandUIManager에 명시적 알림.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardClickHandler : MonoBehaviour,
        IPointerClickHandler,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float DRAG_SCALE = 1.1f;
        private const float DRAG_ALPHA = 0.85f;
        private const float TWEEN_DURATION = 0.12f;

        private CardData _card;
        private int _handIndex;
        private CardGameManager _gameManager;
        private CardHandUIManager _handManager;

        private Canvas _canvas;
        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private LayoutElement _layoutElement;
        private Transform _originalParent;
        private int _originalSiblingIndex;
        private Vector3 _originalScale;

        private GameObject _placeholder;
        private bool _isDragging;
        private int _lastInsertAt = -1;
        private Tween _scaleTween, _alphaTween;

        public void Setup(CardData card, int index, CardGameManager gameManager) =>
            Setup(card, index, gameManager, null);

        public void Setup(CardData card, int index, CardGameManager gameManager, CardHandUIManager handManager)
        {
            _card = card;
            _handIndex = index;
            _gameManager = gameManager;
            _handManager = handManager;

            _rect = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();

            if (_handManager == null) return;
            EnsureDragComponents();
        }

        private void EnsureDragComponents()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            _layoutElement = GetComponent<LayoutElement>();
            if (_layoutElement == null)
            {
                _layoutElement = gameObject.AddComponent<LayoutElement>();
                if (_rect != null)
                {
                    _layoutElement.preferredWidth = _rect.sizeDelta.x;
                    _layoutElement.preferredHeight = _rect.sizeDelta.y;
                }
            }
        }

        private void OnDestroy()
        {
            if (_isDragging)
            {
                CardHandUIManager.EndDragging();
                if (_handManager != null) _handManager.NotifyDragEnd();
                _isDragging = false;
            }
            _scaleTween?.Kill();
            _alphaTween?.Kill();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.dragging || _isDragging) return;
            if (_card == null || _gameManager == null) return;
            _gameManager.RequestDiscard(_card);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_handManager == null || _card == null) return;

            EnsureDragComponents();
            if (_canvas == null || _canvasGroup == null || _layoutElement == null) return;

            _isDragging = true;
            CardHandUIManager.BeginDragging();
            _handManager.NotifyDragStart(gameObject, _card);  // ★ 명시적 알림

            _originalParent = transform.parent;
            _originalSiblingIndex = transform.GetSiblingIndex();
            _originalScale = transform.localScale;
            _lastInsertAt = -1;

            CreatePlaceholder();

            _layoutElement.ignoreLayout = true;
            _canvasGroup.blocksRaycasts = false;

            transform.SetParent(_canvas.transform, true);
            transform.SetAsLastSibling();

            _scaleTween?.Kill();
            _alphaTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale * DRAG_SCALE, TWEEN_DURATION).SetEase(Ease.OutQuad);
            _alphaTween = _canvasGroup.DOFade(DRAG_ALPHA, TWEEN_DURATION);
        }

        private void CreatePlaceholder()
        {
            _placeholder = new GameObject("CardPlaceholder");
            _placeholder.transform.SetParent(_originalParent, false);
            _placeholder.transform.SetSiblingIndex(_originalSiblingIndex);

            var prt = _placeholder.AddComponent<RectTransform>();
            prt.sizeDelta = _rect.sizeDelta;

            var ple = _placeholder.AddComponent<LayoutElement>();
            ple.preferredWidth = _layoutElement.preferredWidth;
            ple.preferredHeight = _layoutElement.preferredHeight;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _rect == null) return;
            _rect.position = eventData.position;
            UpdatePlaceholder(eventData);
        }

        private void UpdatePlaceholder(PointerEventData eventData)
        {
            if (_placeholder == null || _handManager == null) return;

            RectTransform target = _handManager.GetHandContainer();
            if (target == null) target = _originalParent as RectTransform;
            if (target == null) return;

            if (_placeholder.transform.parent != target)
            {
                _placeholder.transform.SetParent(target, false);
                _lastInsertAt = -1;
            }

            int newInsertAt = FindClosestInsertionIndex(target, eventData.position);

            if (newInsertAt != _lastInsertAt)
            {
                _placeholder.transform.SetSiblingIndex(newInsertAt);
                _lastInsertAt = newInsertAt;
            }
        }

        private int FindClosestInsertionIndex(RectTransform container, Vector3 screenPos)
        {
            if (container == null) return 0;

            var cardXs = new List<float>();
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                if (child == null || child.gameObject == _placeholder) continue;
                if (!child.gameObject.activeInHierarchy) continue;
                cardXs.Add(child.position.x);
            }

            if (cardXs.Count == 0) return 0;

            float cardWidth = Mathf.Abs(_rect != null ? _rect.sizeDelta.x : 80f);

            int bestInsertAt = 0;
            float bestDist = Mathf.Abs(screenPos.x - (cardXs[0] - cardWidth * 0.5f));

            for (int i = 0; i < cardXs.Count - 1; i++)
            {
                float midX = (cardXs[i] + cardXs[i + 1]) * 0.5f;
                float d = Mathf.Abs(screenPos.x - midX);
                if (d < bestDist) { bestDist = d; bestInsertAt = i + 1; }
            }

            float rightX = cardXs[cardXs.Count - 1] + cardWidth * 0.5f;
            float rightDist = Mathf.Abs(screenPos.x - rightX);
            if (rightDist < bestDist) { bestInsertAt = cardXs.Count; }

            return bestInsertAt;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;
            _isDragging = false;
            CardHandUIManager.EndDragging();
            _handManager.NotifyDragEnd();  // ★ 명시적 알림

            if (_card == null)
            {
                HideAndKillTweens();
                return;
            }

            RectTransform targetContainer = _placeholder != null
                ? _placeholder.transform.parent as RectTransform
                : _originalParent as RectTransform;

            int finalIndex = _placeholder != null
                ? _placeholder.transform.GetSiblingIndex()
                : _originalSiblingIndex;

            HideAndKillTweens();

            if (targetContainer != null && _handManager != null)
                _handManager.HandleDrop(_card, targetContainer, finalIndex);
        }

        private void HideAndKillTweens()
        {
            _scaleTween?.Kill();
            _alphaTween?.Kill();

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
            }

            if (_placeholder != null)
            {
                Destroy(_placeholder);
                _placeholder = null;
            }

            _lastInsertAt = -1;
        }
    }
}