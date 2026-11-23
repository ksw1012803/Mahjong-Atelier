using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using CardGame.Core;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CardGame.UI3D
{
    /// <summary>
    /// 3D 손패 매니저.
    /// 
    /// 사용자 손패를 3D 공간에 배치. 마우스 레이캐스트로 클릭 처리.
    /// 
    /// 구조:
    ///   HandAnchor (사용자 앞 3D 위치)
    ///   ├── Card_0 (Card3DView + Card3DClickHandler)
    ///   ├── Card_1
    ///   └── ...
    /// </summary>
    public class Card3DHandManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CardGameManager gameManager;

        [Tooltip("사용자 손패가 놓일 3D 위치 (Empty GameObject)")]
        [SerializeField] private Transform handAnchor;

        [Tooltip("뽑은 카드 강조 위치 (선택)")]
        [SerializeField] private Transform drawnCardAnchor;

        [Header("Fallback Prefab")]
        [Tooltip("Wild/미제작 카드용 대체 프리팹. 비워두면 표시 안 됨")]
        [SerializeField] private GameObject fallbackCardPrefab;

        [Header("Layout Settings")]
        [Tooltip("카드 간 간격 (X축)")]
        [SerializeField] private float cardSpacing = 0.15f;

        [Tooltip("카드 회전 (플레이어 방향 보게, X축 회전)")]
        [SerializeField] private Vector3 cardRotation = new Vector3(60f, 0f, 0f);

        [Tooltip("손패 부채꼴 각도 (0이면 일직선)")]
        [SerializeField, Range(0f, 30f)] private float fanAngle = 5f;

        [Tooltip("카드 스케일")]
        [SerializeField] private float cardScale = 1f;

        [Header("Interaction")]
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private LayerMask cardLayerMask = ~0;
        [SerializeField] private float rayMaxDistance = 100f;

        private readonly List<GameObject> _spawnedCards = new List<GameObject>();
        private CardData _lastDrawnCard;
        private Card3DClickHandler _currentHovered;

        private void OnEnable()
        {
            CardGameEvents.OnHandChanged += HandleHandChanged;
            CardGameEvents.OnCardDrawn += HandleCardDrawn;
            CardGameEvents.OnDealCompleted += HandleDealCompleted;
            CardGameEvents.OnCompletion += HandleRoundCompleted;
        }

        private void OnDisable()
        {
            CardGameEvents.OnHandChanged -= HandleHandChanged;
            CardGameEvents.OnCardDrawn -= HandleCardDrawn;
            CardGameEvents.OnDealCompleted -= HandleDealCompleted;
            CardGameEvents.OnCompletion -= HandleRoundCompleted;
        }

        private void Awake()
        {
            if (raycastCamera == null) raycastCamera = Camera.main;
        }

        private void Update()
        {
            HandleMouseInput();
        }

        private void HandleMouseInput()
        {
            if (raycastCamera == null) return;

            Vector2 mousePos;
            bool clicked;

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;
            mousePos = mouse.position.ReadValue();
            clicked = mouse.leftButton.wasPressedThisFrame;
#else
            mousePos = Input.mousePosition;
            clicked = Input.GetMouseButtonDown(0);
#endif

            var ray = raycastCamera.ScreenPointToRay(mousePos);
            Card3DClickHandler hovered = null;

            if (Physics.Raycast(ray, out var hit, rayMaxDistance, cardLayerMask))
            {
                // Card3DClickHandler를 자신 또는 부모에서 찾기
                hovered = hit.collider.GetComponentInParent<Card3DClickHandler>();
            }

            // 호버 상태 업데이트
            if (hovered != _currentHovered)
            {
                if (_currentHovered != null) _currentHovered.SetHovering(false);
                if (hovered != null) hovered.SetHovering(true);
                _currentHovered = hovered;
            }

            // 클릭 처리
            if (clicked && hovered != null)
            {
                hovered.TriggerClick();
            }
        }

        private void HandleDealCompleted()
        {
            _lastDrawnCard = null;
            RefreshDisplay();
        }

        /// <summary>라운드 종료 → 3D 손패를 작아지며 사라지게 (다음 배패 전 테이블 정리).</summary>
        private void HandleRoundCompleted(int playerIndex, CompletionResult result)
        {
            _lastDrawnCard = null;
            var leaving = new List<GameObject>(_spawnedCards);
            _spawnedCards.Clear();
            for (int i = 0; i < leaving.Count; i++)
            {
                var go = leaving[i];
                if (go == null) continue;
                go.transform.DOKill();
                go.transform.DOScale(Vector3.zero, 0.3f).SetDelay(0.5f + i * 0.03f).SetEase(Ease.InBack)
                    .SetLink(go).OnComplete(() => { if (go != null) Destroy(go); });
            }
        }

        private void HandleHandChanged(int playerIndex)
        {
            if (gameManager == null) return;
            if (playerIndex != gameManager.UserPlayerIndex) return;
            RefreshDisplay();
        }

        private void HandleCardDrawn(int playerIndex, CardData card)
        {
            if (gameManager == null) return;
            if (playerIndex != gameManager.UserPlayerIndex) return;
            _lastDrawnCard = card;
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            ClearSpawned();
            if (gameManager == null || handAnchor == null) return;

            var user = gameManager.Players[gameManager.UserPlayerIndex];
            var hand = new List<CardData>(user.Hand);

            CardData drawn = null;
            if (_lastDrawnCard != null && hand.Contains(_lastDrawnCard) && drawnCardAnchor != null)
            {
                drawn = _lastDrawnCard;
                hand.Remove(drawn);
            }

            // 손패 배치
            LayoutCards(hand, handAnchor);

            // 뽑은 카드 강조
            if (drawn != null && drawnCardAnchor != null)
            {
                var singleList = new List<CardData> { drawn };
                LayoutCards(singleList, drawnCardAnchor);
            }
        }

        private void LayoutCards(List<CardData> cards, Transform anchor)
        {
            int count = cards.Count;
            if (count == 0) return;

            // 중앙 기준 배치
            float totalWidth = (count - 1) * cardSpacing;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                var card = cards[i];
                var go = SpawnCard(card, anchor);
                if (go == null) continue;

                float t = count > 1 ? (float)i / (count - 1) : 0.5f;
                float x = startX + i * cardSpacing;

                // 부채꼴 회전 (중앙이 0도, 좌우로 벌어짐)
                float fanRot = fanAngle * (t - 0.5f) * 2f;

                go.transform.localPosition = new Vector3(x, 0, 0);
                go.transform.localEulerAngles = cardRotation + new Vector3(0, 0, -fanRot);
                go.transform.localScale = Vector3.one * cardScale;

                // 원본 위치 캡처 (호버 이펙트용)
                var click = go.GetComponent<Card3DClickHandler>();
                if (click != null) click.UpdateOriginalPosition();
            }
        }

        private GameObject SpawnCard(CardData card, Transform parent)
        {
            var prefab = Card3DResourceMapper.LoadPrefab(card);
            if (prefab == null) prefab = fallbackCardPrefab;

            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, parent);
            }
            else
            {
                // 프리팹도 없으면 빈 GameObject
                go = new GameObject($"Card_{card}");
                go.transform.SetParent(parent, false);
            }

            _spawnedCards.Add(go);

            // Card3DView + Collider + ClickHandler
            var view = go.GetComponent<Card3DView>();
            if (view == null) view = go.AddComponent<Card3DView>();
            view.SetCard(card, false);

            // Collider 확인 (레이캐스트용)
            EnsureCollider(go);

            var click = go.GetComponent<Card3DClickHandler>();
            if (click == null) click = go.AddComponent<Card3DClickHandler>();
            click.Setup(card, HandleCardClicked);

            return go;
        }

        private void EnsureCollider(GameObject go)
        {
            // 이미 어디든 Collider 있으면 스킵
            if (go.GetComponentInChildren<Collider>() != null) return;

            // MeshFilter 있으면 그 크기로 BoxCollider 자동 추가
            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var bounds = mf.sharedMesh.bounds;
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                bc.center = bounds.center;
                bc.size = bounds.size;
            }
            else
            {
                // 기본 BoxCollider
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(0.1f, 0.15f, 0.01f);
            }
        }

        private void HandleCardClicked(CardData card)
        {
            if (gameManager == null) return;
            Debug.Log($"[Card3DHand] 카드 클릭: {card}");
            gameManager.RequestDiscard(card);
        }

        private void ClearSpawned()
        {
            _currentHovered = null;
            foreach (var go in _spawnedCards)
                if (go != null) Destroy(go);
            _spawnedCards.Clear();
        }
    }
}
