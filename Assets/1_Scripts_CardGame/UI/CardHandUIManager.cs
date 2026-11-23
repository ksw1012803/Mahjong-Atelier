using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CardGame.UI
{
    /// <summary>
    /// 사용자 손패 UI v9.13 — DOTween SetLink로 Scene 전환 시 자동 cleanup.
    ///
    /// v9.13 변경:
    ///   - 모든 트윈/시퀀스에 SetLink(targetGO) 추가 → 대상 Destroy 시 자동 Kill.
    ///   - OnDisable에서 _spawned의 모든 카드 DOKill 호출 → 전환 직전 안전 처리.
    ///   - "CanvasGroup has been destroyed" DOTween 에러 로그 제거.
    /// </summary>
    public class CardHandUIManager : MonoBehaviour
    {
        private static int _activeDragCount = 0;
        public static bool IsAnyDragging => _activeDragCount > 0;
        public static void BeginDragging() => _activeDragCount++;
        public static void EndDragging() => _activeDragCount = Mathf.Max(0, _activeDragCount - 1);

        [Header("References")]
        [SerializeField] private CardGameManager gameManager;
        [SerializeField] private RectTransform handContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private RectTransform deckPosition;

        [Header("Card Layout")]
        [SerializeField] private float cardWidth = 80f;
        [SerializeField] private float cardHeight = 120f;
        [SerializeField] private float overlapAmount = 30f;
        [SerializeField] private float cardTiltAngle = 5f;

        [Header("Hover Effect")]
        [SerializeField] private float hoverLift = 40f;
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float hoverPush = 24f;
        [SerializeField] private float hoverDuration = 0.18f;

        [Header("Drop Return")]
        [SerializeField] private float dropReturnDuration = 0.25f;

        [Header("Animation")]
        [SerializeField] private float drawDuration = 0.4f;
        [SerializeField] private float dealStagger = 0.08f;
        [SerializeField] private float dealDuration = 0.35f;
        [SerializeField] private Ease drawEase = Ease.OutBack;

        [Header("Settings")]
        [SerializeField] private bool autoSort = false;

        [Header("Round End (라운드 종료 시 손패 정리)")]
        [Tooltip("승리가 확정된 뒤 손패를 치우기 시작할 때까지 대기(초). 마지막 패를 잠깐 보여줌.")]
        [SerializeField] private float roundEndClearDelay = 0.5f;
        [Tooltip("손패가 아래로 내려가며 사라지는 시간(초).")]
        [SerializeField] private float roundEndClearDuration = 0.35f;
        [SerializeField] private float roundEndDropDistance = 160f;
        [SerializeField] private float roundEndStagger = 0.04f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private Canvas _canvas;

        private GameObject _currentDraggingGO;
        private CardData _currentDraggingCard;
        private GameObject _hoveredCard;

        private int _dragInsertIndex = -1;

        public RectTransform GetHandContainer() => handContainer;
        public RectTransform GetDrawnContainer() => null;

        public void NotifyDragStart(GameObject go, CardData card)
        {
            _currentDraggingGO = go;
            _currentDraggingCard = card;

            if (go != null) go.transform.DOKill();
            _hoveredCard = null;
            _dragInsertIndex = _spawned.IndexOf(go);

            RebuildHandLayoutAnimated();
        }

        public void NotifyDragEnd()
        {
            _currentDraggingGO = null;
            _currentDraggingCard = null;
        }

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null) _canvas = FindObjectOfType<Canvas>();
        }

        private void OnEnable()
        {
            CardGameEvents.OnHandChanged += OnHandChanged;
            CardGameEvents.OnCardDrawn += OnCardDrawn;
            CardGameEvents.OnDealCompleted += OnDealCompleted;
            CardGameEvents.OnCompletion += OnRoundCompleted;
        }

        private void OnDisable()
        {
            CardGameEvents.OnHandChanged -= OnHandChanged;
            CardGameEvents.OnCardDrawn -= OnCardDrawn;
            CardGameEvents.OnDealCompleted -= OnDealCompleted;
            CardGameEvents.OnCompletion -= OnRoundCompleted;

            // ★ v9.13: Scene 전환 전 모든 트윈 명시적 Kill
            KillAllCardTweens();
        }

        private void OnDestroy()
        {
            KillAllCardTweens();
        }

        /// <summary>_spawned의 모든 카드 트윈 + Clone 트윈 즉시 Kill.</summary>
        private void KillAllCardTweens()
        {
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                go.transform.DOKill();
                var cg = go.GetComponent<CanvasGroup>();
                if (cg != null) cg.DOKill();
            }
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
#else
            if (Input.GetKeyDown(KeyCode.F1))
#endif
            {
                autoSort = !autoSort;
                RefreshDisplay();
            }

            if (IsAnyDragging && _currentDraggingGO != null)
            {
                UpdateDragInsertIndex();
            }
        }

        private void OnDealCompleted()
        {
            RefreshDisplayHidden();
            AnimateInitialDeal();
        }

        /// <summary>
        /// 라운드 종료(승리 확정) → 남은 손패를 아래로 내리며 페이드아웃 후 제거.
        /// 다음 라운드 배패가 빈 손에서 시작되도록 해서 "이전 패가 잠깐 남아 있는" 현상 제거.
        /// </summary>
        private void OnRoundCompleted(int playerIndex, CompletionResult result)
        {
            ClearHandAnimated();
        }

        private void ClearHandAnimated()
        {
            if (_spawned.Count == 0) return;

            var leaving = new List<GameObject>(_spawned);
            _spawned.Clear();
            _hoveredCard = null;

            for (int i = 0; i < leaving.Count; i++)
            {
                var go = leaving[i];
                if (go == null) continue;
                go.transform.DOKill();

                var cg = go.GetComponent<CanvasGroup>();
                if (cg == null) cg = go.AddComponent<CanvasGroup>();
                cg.DOKill();
                cg.blocksRaycasts = false;
                cg.interactable = false;

                var rt = go.transform as RectTransform;
                float delay = roundEndClearDelay + i * roundEndStagger;

                var seq = DOTween.Sequence().SetLink(go);
                seq.AppendInterval(delay);
                if (rt != null)
                    seq.Append(rt.DOAnchorPosY(rt.anchoredPosition.y - roundEndDropDistance, roundEndClearDuration).SetEase(Ease.InQuad));
                seq.Join(cg.DOFade(0f, roundEndClearDuration).SetEase(Ease.InQuad));
                seq.OnComplete(() => { if (go != null) Destroy(go); });
            }
        }

        private void OnHandChanged(int playerIndex)
        {
            if (gameManager == null || playerIndex != gameManager.UserPlayerIndex) return;
            if (gameManager.CurrentPhase == GamePhase.Dealing) return;
            RefreshDisplay();
        }

        private void OnCardDrawn(int playerIndex, CardData card)
        {
            if (gameManager == null || playerIndex != gameManager.UserPlayerIndex) return;
            if (gameManager.CurrentPhase == GamePhase.Dealing) return;
            RefreshDisplay();
            AnimateDraw(card);
        }

        public void RefreshDisplay() => RefreshInternal(hideAll: false);
        private void RefreshDisplayHidden() => RefreshInternal(hideAll: true);

        private void RefreshInternal(bool hideAll)
        {
            if (gameManager == null || cardPrefab == null || handContainer == null) return;

            GameObject draggingGO = _currentDraggingGO;
            CardData draggingCard = _currentDraggingCard;

            for (int i = _spawned.Count - 1; i >= 0; i--)
            {
                var go = _spawned[i];
                if (go == null) { _spawned.RemoveAt(i); continue; }
                if (go == draggingGO) continue;
                go.transform.DOKill();
                go.SetActive(false);
                Destroy(go);
                _spawned.RemoveAt(i);
            }

            if (_hoveredCard == null || !_spawned.Contains(_hoveredCard))
                _hoveredCard = null;

            var user = gameManager.Players[gameManager.UserPlayerIndex];
            var hand = new List<CardData>(user.Hand);

            if (autoSort)
            {
                hand.Sort((a, b) =>
                {
                    if (a.Kind.IsSpecial != b.Kind.IsSpecial) return a.Kind.IsSpecial ? 1 : -1;
                    int c = ((int)a.Kind.Color).CompareTo((int)b.Kind.Color);
                    return c != 0 ? c : a.Kind.Number.CompareTo(b.Kind.Number);
                });
            }

            int spawnIndex = 0;
            for (int i = 0; i < hand.Count; i++)
            {
                if (draggingCard != null && hand[i] == draggingCard) continue;
                var go = Spawn(hand[i], handContainer, spawnIndex);

                if (hideAll && go != null)
                {
                    var cg = go.GetComponent<CanvasGroup>();
                    if (cg != null) cg.alpha = 0f;
                }

                spawnIndex++;
            }

            RebuildHandLayoutInstant();
        }

        private GameObject Spawn(CardData card, RectTransform parent, int handIndex)
        {
            var go = Instantiate(cardPrefab, parent);
            go.SetActive(true);
            _spawned.Add(go);

            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
            le.preferredWidth = cardWidth;
            le.preferredHeight = cardHeight;

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(cardWidth, cardHeight);
                rt.localScale = Vector3.one;
            }

            if (go.GetComponent<CanvasGroup>() == null)
                go.AddComponent<CanvasGroup>();

            var view = go.GetComponent<CardView>();
            if (view != null) view.SetCard(card);

            var click = go.GetComponent<CardClickHandler>() ?? go.AddComponent<CardClickHandler>();
            click.Setup(card, handIndex, gameManager, this);

            var hover = go.GetComponent<CardHoverHandler>() ?? go.AddComponent<CardHoverHandler>();
            hover.Setup(this);

            return go;
        }

        // ========== 수동 배치 ==========

        private void RebuildHandLayout(bool animated, float duration = -1f)
        {
            int count = _spawned.Count;
            if (count == 0) return;

            int hoverIndex = (_hoveredCard != null) ? _spawned.IndexOf(_hoveredCard) : -1;
            int draggedIndex = (_currentDraggingGO != null) ? _spawned.IndexOf(_currentDraggingGO) : -1;

            float spacing = cardWidth - overlapAmount;
            float startX = -((count - 1) * spacing) * 0.5f;

            int insertAt = -1;
            if (draggedIndex >= 0 && _dragInsertIndex >= 0)
            {
                insertAt = Mathf.Clamp(_dragInsertIndex, 0, count - 1);
            }

            float tweenDur = (duration > 0f) ? duration : hoverDuration;

            int visualIdx = 0;
            for (int i = 0; i < count; i++)
            {
                var go = _spawned[i];
                if (go == null) continue;
                if (i == draggedIndex) continue;

                var rt = go.GetComponent<RectTransform>();
                if (rt == null) continue;

                int slot = visualIdx;
                if (insertAt >= 0 && slot >= insertAt) slot = visualIdx + 1;
                visualIdx++;

                float x = startX + slot * spacing;
                float y = 0f;
                float scaleVal = 1f;
                float rotVal = cardTiltAngle;

                if (draggedIndex < 0 && hoverIndex >= 0)
                {
                    if (i == hoverIndex)
                    {
                        y = hoverLift;
                        scaleVal = hoverScale;
                        rotVal = 0f;
                    }
                    else if (i < hoverIndex) x -= hoverPush;
                    else x += hoverPush;
                }

                rt.DOKill();
                if (animated)
                {
                    // ★ v9.13: SetLink로 target destroy 시 자동 Kill
                    rt.DOAnchorPos(new Vector2(x, y), tweenDur).SetEase(Ease.OutQuad).SetLink(go);
                    rt.DOScale(Vector3.one * scaleVal, tweenDur).SetEase(Ease.OutQuad).SetLink(go);
                    rt.DOLocalRotate(new Vector3(0f, 0f, rotVal), tweenDur).SetEase(Ease.OutQuad).SetLink(go);
                }
                else
                {
                    rt.anchoredPosition = new Vector2(x, y);
                    rt.localScale = Vector3.one * scaleVal;
                    rt.localRotation = Quaternion.Euler(0f, 0f, rotVal);
                }
            }

            if (hoverIndex >= 0 && draggedIndex < 0 && _spawned[hoverIndex] != null)
                _spawned[hoverIndex].transform.SetAsLastSibling();

            if (draggedIndex >= 0 && _spawned[draggedIndex] != null)
                _spawned[draggedIndex].transform.SetAsLastSibling();
        }

        private void RebuildHandLayoutInstant() => RebuildHandLayout(false);
        private void RebuildHandLayoutAnimated() => RebuildHandLayout(true);
        private void RebuildHandLayoutAnimated(float duration) => RebuildHandLayout(true, duration);

        // ========== 드래그 중 삽입 인덱스 추적 ==========

        private void UpdateDragInsertIndex()
        {
            if (_currentDraggingGO == null || handContainer == null) return;

            Vector3 worldPos = _currentDraggingGO.transform.position;
            Vector3 localPos = handContainer.InverseTransformPoint(worldPos);
            float pointerX = localPos.x;

            int newInsert = CalculateInsertIndex(pointerX);
            if (newInsert != _dragInsertIndex)
            {
                _dragInsertIndex = newInsert;
                RebuildHandLayoutAnimated();
            }
        }

        private int CalculateInsertIndex(float pointerX)
        {
            int count = _spawned.Count;
            if (count <= 1) return 0;

            float spacing = cardWidth - overlapAmount;
            float startX = -((count - 1) * spacing) * 0.5f;

            int bestSlot = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float slotX = startX + i * spacing;
                float dist = Mathf.Abs(pointerX - slotX);
                if (dist < minDist)
                {
                    minDist = dist;
                    bestSlot = i;
                }
            }
            return bestSlot;
        }

        // ========== Hover API ==========

        public void OnHoverStart(GameObject card)
        {
            if (card == null) return;
            if (IsAnyDragging) return;
            if (_hoveredCard == card) return;
            if (!_spawned.Contains(card)) return;

            _hoveredCard = card;
            RebuildHandLayoutAnimated();
        }

        public void OnHoverEnd(GameObject card)
        {
            if (_hoveredCard != card) return;
            _hoveredCard = null;
            RebuildHandLayoutAnimated();
        }

        // ========== Drop ==========

        public void HandleDrop(CardData card, RectTransform targetContainer, int targetIndex)
        {
            if (gameManager == null || card == null) return;

            var user = gameManager.Players[gameManager.UserPlayerIndex];
            if (user == null || !user.Hand.Contains(card)) return;

            int oldIndex = user.Hand.IndexOf(card);
            int newIndex = (_dragInsertIndex >= 0)
                ? Mathf.Clamp(_dragInsertIndex, 0, user.Hand.Count - 1)
                : Mathf.Clamp(targetIndex, 0, user.Hand.Count - 1);

            _dragInsertIndex = -1;

            if (oldIndex != newIndex)
            {
                user.ReorderCard(card, newIndex);
                Debug.Log($"[HandUI] 순서 변경: {card} ({oldIndex} → {newIndex})");
            }

            ReorderSpawnedToMatchHand();
            RestoreCardsAfterDrop();
            RebuildHandLayoutAnimated(dropReturnDuration);

            gameManager.TryUserCompletionCheck();
        }

        private void ReorderSpawnedToMatchHand()
        {
            var user = gameManager.Players[gameManager.UserPlayerIndex];
            if (user == null) return;

            var newOrder = new List<GameObject>(user.Hand.Count);
            foreach (var cd in user.Hand)
            {
                GameObject found = null;
                foreach (var go in _spawned)
                {
                    if (go == null) continue;
                    var view = go.GetComponent<CardView>();
                    if (view != null && view.CardData == cd) { found = go; break; }
                }
                if (found != null) newOrder.Add(found);
            }

            _spawned.Clear();
            _spawned.AddRange(newOrder);
        }

        private void RestoreCardsAfterDrop()
        {
            if (handContainer == null) return;

            foreach (var go in _spawned)
            {
                if (go == null) continue;

                if (go.transform.parent != handContainer)
                    go.transform.SetParent(handContainer, worldPositionStays: true);

                var cg = go.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }

                var rt = go.GetComponent<RectTransform>();
                if (rt != null && rt.localScale == Vector3.zero)
                    rt.localScale = Vector3.one;
            }
        }

        // ========== 배패/뽑기 애니메이션 ==========

        private void AnimateInitialDeal()
        {
            if (handContainer == null) return;

            DOVirtual.DelayedCall(0f, () =>
            {
                if (this == null) return;  // Scene 전환 시 안전
                RebuildHandLayoutInstant();

                for (int i = 0; i < _spawned.Count; i++)
                {
                    var go = _spawned[i];
                    if (go == null) continue;
                    var rt = go.GetComponent<RectTransform>();
                    if (rt == null) continue;

                    AnimateCardWithClone(rt, i * dealStagger, dealDuration, 0.6f);
                }
            }, ignoreTimeScale: true);
        }

        private void AnimateDraw(CardData card)
        {
            DOVirtual.DelayedCall(0f, () =>
            {
                if (this == null) return;
                var rt = FindCardRect(card);
                if (rt != null) AnimateCardWithClone(rt, 0f, drawDuration, 0.5f);
            }, ignoreTimeScale: true);
        }

        private void AnimateCardWithClone(RectTransform original, float delay, float duration, float startScale)
        {
            if (original == null) return;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            if (_canvas == null) return;

            Vector3 endPos = original.position;
            Vector3 endScale = original.localScale;
            Quaternion endRotation = original.localRotation;
            Vector2 cardSize = original.sizeDelta;

            var originalCg = original.GetComponent<CanvasGroup>();
            if (originalCg == null) originalCg = original.gameObject.AddComponent<CanvasGroup>();
            originalCg.alpha = 0f;

            var cloneGo = Instantiate(original.gameObject, _canvas.transform);
            cloneGo.name = "DrawAnim_Clone";

            var cloneClick = cloneGo.GetComponent<CardClickHandler>();
            if (cloneClick != null) Destroy(cloneClick);
            var cloneHover = cloneGo.GetComponent<CardHoverHandler>();
            if (cloneHover != null) Destroy(cloneHover);
            var cloneLE = cloneGo.GetComponent<LayoutElement>();
            if (cloneLE != null) cloneLE.ignoreLayout = true;

            var cloneRt = cloneGo.GetComponent<RectTransform>();
            cloneRt.sizeDelta = cardSize;

            Vector3 startPos = deckPosition != null
                ? deckPosition.position
                : endPos + new Vector3(0f, Screen.height * 0.3f, 0f);

            cloneRt.position = startPos;
            cloneRt.localScale = endScale * startScale;
            cloneRt.localRotation = Quaternion.identity;

            var cloneCg = cloneGo.GetComponent<CanvasGroup>();
            if (cloneCg == null) cloneCg = cloneGo.AddComponent<CanvasGroup>();
            cloneCg.alpha = 0f;
            cloneCg.blocksRaycasts = false;
            cloneCg.interactable = false;

            // ★ v9.13: Sequence 전체에 SetLink(cloneGo) → Clone이 Destroy되면 Sequence 자동 Kill
            var seq = DOTween.Sequence().SetLink(cloneGo).SetDelay(delay);
            seq.AppendCallback(() => { if (cloneCg != null) cloneCg.alpha = 1f; });
            seq.Append(cloneRt.DOMove(endPos, duration).SetEase(Ease.OutCubic).SetLink(cloneGo));
            seq.Join(cloneRt.DOScale(endScale, duration).SetEase(drawEase).SetLink(cloneGo));
            seq.Join(cloneRt.DOLocalRotateQuaternion(endRotation, duration).SetEase(Ease.OutCubic).SetLink(cloneGo));
            seq.OnComplete(() =>
            {
                if (originalCg != null) originalCg.alpha = 1f;
                if (cloneGo != null) Destroy(cloneGo);
            });
        }

        private RectTransform FindCardRect(CardData card)
        {
            foreach (var go in _spawned)
            {
                if (go == null) continue;
                var view = go.GetComponent<CardView>();
                if (view != null && view.CardData == card)
                    return go.GetComponent<RectTransform>();
            }
            return null;
        }

        public int FindClosestHandIndex(Vector3 screenPos) => 0;
        public void ReorderHand(CardData card, int newIndex) => HandleDrop(card, handContainer, newIndex);
    }
}