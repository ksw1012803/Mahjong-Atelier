using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using CardGame.Core;

namespace CardGame.UI3D
{
    /// <summary>
    /// 테이블 중앙 공용 버림패 더미 (모든 플레이어의 버린 카드가 한곳에 쌓임).
    ///
    ///   - 새 카드가 버려지면 위에서 떨어지며 약간 흐트러진 각도/위치로 안착 (실제 더미 느낌)
    ///   - 마우스 호버 시 살짝 커짐, 클릭 시 DiscardHistoryPanel 열림
    ///   - 성능: maxVisibleCards 개까지만 3D 오브젝트 유지 (아래 깔린 건 제거). 데이터는 패널이 전부 보관
    ///
    /// 세팅:
    ///   1. Main Camera에 Physics Raycaster 추가 (3D 오브젝트가 EventSystem 클릭을 받기 위해 필수)
    ///   2. 테이블 중앙에 빈 GameObject → 이 컴포넌트 추가. 자식으로 PileAnchor 빈 오브젝트 → pileAnchor에 연결
    ///   3. BoxCollider는 autoCollider=true면 자동 생성/크기 조정
    ///   4. 기존 Card3DDiscardPileView 4개는 비활성화 (또는 삭제)
    /// </summary>
    public class Card3DCentralDiscardPile : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("References")]
        [SerializeField] private Transform pileAnchor;
        [SerializeField] private GameObject fallbackPrefab;
        [Tooltip("클릭 시 열 패널. 비워두면 onClicked 이벤트만 발생.")]
        [SerializeField] private CardGame.UI.DiscardHistoryPanel historyPanel;

        [Header("Stack")]
        [Tooltip("카드 1장당 높이 증가 (m). 카드 두께 정도.")]
        [SerializeField] private float stackHeightPerCard = 0.0025f;
        [Tooltip("중심에서 흐트러지는 반경 (m).")]
        [SerializeField] private float scatterRadius = 0.03f;
        [Tooltip("흐트러지는 회전 범위 (±deg).")]
        [SerializeField] private float scatterAngle = 25f;
        [Tooltip("흐트러짐 회전 축 (pileAnchor 로컬). 테이블이 XZ 평면이면 Y축.")]
        [SerializeField] private Vector3 scatterAxis = Vector3.up;
        [Tooltip("3D로 유지할 최대 장수. 넘으면 가장 아래(오래된) 카드부터 제거.")]
        [SerializeField, Range(5, 200)] private int maxVisibleCards = 40;

        [Header("Prefab Transform")]
        [SerializeField] private bool preservePrefabScale = true;
        [SerializeField] private bool preservePrefabRotation = true;
        [SerializeField] private float scaleMultiplier = 1f;
        [SerializeField] private Vector3 additionalRotation = Vector3.zero;
        [SerializeField] private float baseYOffset = 0.01f;

        [Header("Toss Animation")]
        [SerializeField] private bool animateToss = true;
        [SerializeField] private float tossHeight = 0.25f;
        [SerializeField] private float tossDuration = 0.28f;
        [SerializeField] private Ease tossEase = Ease.OutQuad;
        [Tooltip("던져질 때 추가 회전 (deg). 0이면 없음.")]
        [SerializeField] private float tossSpin = 180f;

        [Header("Hover / Click")]
        [SerializeField] private bool autoCollider = true;
        [Tooltip("자동 생성 콜라이더 크기 (pileAnchor 로컬, m). 카드 더미보다 넉넉하게.")]
        [SerializeField] private Vector3 colliderSize = new Vector3(0.25f, 0.15f, 0.25f);
        [SerializeField] private float hoverScale = 1.06f;
        [SerializeField] private float hoverDuration = 0.15f;
        [Tooltip("클릭 시 추가로 실행할 것 (사운드 등).")]
        public UnityEvent onClicked;

        private static CardDatabase _database;
        public static void SetDatabase(CardDatabase db) => _database = db;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private int _totalCount;
        private Vector3 _anchorBaseScale = Vector3.one;
        private BoxCollider _collider;

        public int TotalCount => _totalCount;

        // =====================================================================

        private void Awake()
        {
            if (pileAnchor == null) pileAnchor = transform;
            _anchorBaseScale = pileAnchor.localScale;

            if (autoCollider)
            {
                _collider = GetComponent<BoxCollider>();
                if (_collider == null) _collider = gameObject.AddComponent<BoxCollider>();
                _collider.isTrigger = true;
                UpdateColliderSize();
            }
        }

        private void OnEnable()
        {
            CardGameEvents.OnCardDiscarded += HandleDiscarded;
            CardGameEvents.OnDealStarted += HandleDealStarted;
        }

        private void OnDisable()
        {
            CardGameEvents.OnCardDiscarded -= HandleDiscarded;
            CardGameEvents.OnDealStarted -= HandleDealStarted;
            pileAnchor?.DOKill();
        }

        private void OnDestroy()
        {
            foreach (var go in _spawned) if (go != null) go.transform.DOKill();
        }

        // =====================================================================
        //  Events
        // =====================================================================

        private void HandleDealStarted()
        {
            ClearAll();
        }

        private void HandleDiscarded(int fromPlayerIndex, CardData card)
        {
            if (card == null || card.IsPlaceholder) return;
            SpawnOnTop(card, fromPlayerIndex);
        }

        // =====================================================================
        //  Spawn
        // =====================================================================

        private void SpawnOnTop(CardData card, int fromSeat)
        {
            GameObject prefab = null;
            if (_database != null && card.Definition != null)
                prefab = _database.GetPrefab(card.Definition);
            if (prefab == null) prefab = fallbackPrefab;
            if (prefab == null) return;

            var prefabScale = prefab.transform.localScale;
            var prefabRot = prefab.transform.localRotation;

            var go = Instantiate(prefab, pileAnchor);
            go.name = $"Discard_{_totalCount:000}_P{fromSeat}_{card}";
            _spawned.Add(go);
            _totalCount++;

            // 더미 위 목표 위치/회전 (약간 흐트러짐)
            Vector2 r = Random.insideUnitCircle * scatterRadius;
            float y = baseYOffset + (_spawned.Count - 1) * stackHeightPerCard;
            Vector3 targetPos = new Vector3(r.x, y, r.y);

            Quaternion baseRot = preservePrefabRotation
                ? prefabRot * Quaternion.Euler(additionalRotation)
                : Quaternion.Euler(additionalRotation);
            float angle = Random.Range(-scatterAngle, scatterAngle);
            Quaternion targetRot = Quaternion.AngleAxis(angle, scatterAxis.normalized) * baseRot;

            go.transform.localScale = preservePrefabScale
                ? prefabScale * scaleMultiplier
                : Vector3.one * scaleMultiplier;

            foreach (var col in go.GetComponentsInChildren<Collider>()) col.enabled = false;

            if (animateToss)
            {
                go.transform.localPosition = targetPos + new Vector3(0f, tossHeight, 0f);
                float spinStart = angle + (tossSpin != 0f ? Random.Range(-tossSpin, tossSpin) : 0f);
                go.transform.localRotation = Quaternion.AngleAxis(spinStart, scatterAxis.normalized) * baseRot;

                var seq = DOTween.Sequence().SetLink(go);
                seq.Append(go.transform.DOLocalMove(targetPos, tossDuration).SetEase(tossEase));
                seq.Join(go.transform.DOLocalRotateQuaternion(targetRot, tossDuration).SetEase(Ease.OutCubic));
            }
            else
            {
                go.transform.localPosition = targetPos;
                go.transform.localRotation = targetRot;
            }

            TrimBottom();
            UpdateColliderSize();
        }

        /// <summary>가시 한도 초과 시 가장 아래 카드부터 제거 (위치는 그대로 둬서 더미 높이 유지).</summary>
        private void TrimBottom()
        {
            while (_spawned.Count > maxVisibleCards)
            {
                var oldest = _spawned[0];
                _spawned.RemoveAt(0);
                if (oldest != null) { oldest.transform.DOKill(); Destroy(oldest); }
            }
        }

        private void ClearAll()
        {
            foreach (var go in _spawned)
                if (go != null) { go.transform.DOKill(); Destroy(go); }
            _spawned.Clear();
            _totalCount = 0;
            UpdateColliderSize();
        }

        private void UpdateColliderSize()
        {
            if (_collider == null) return;
            float h = Mathf.Max(colliderSize.y, baseYOffset + _spawned.Count * stackHeightPerCard + 0.02f);
            _collider.size = new Vector3(colliderSize.x, h, colliderSize.z);
            _collider.center = new Vector3(0f, h * 0.5f, 0f);
        }

        // =====================================================================
        //  Pointer (Physics Raycaster 필요)
        // =====================================================================

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (pileAnchor == null) return;
            pileAnchor.DOKill();
            pileAnchor.DOScale(_anchorBaseScale * hoverScale, hoverDuration).SetEase(Ease.OutQuad).SetLink(pileAnchor.gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (pileAnchor == null) return;
            pileAnchor.DOKill();
            pileAnchor.DOScale(_anchorBaseScale, hoverDuration).SetEase(Ease.OutQuad).SetLink(pileAnchor.gameObject);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            onClicked?.Invoke();
            if (historyPanel != null) historyPanel.Toggle();
        }

        [ContextMenu("Debug: Clear Pile")]
        private void DebugClear() => ClearAll();
    }
}
