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

        [Header("Editor Gizmo (에디터 전용, 빌드에 영향 없음)")]
        [Tooltip("씬 뷰에 버림패 더미 미리보기 표시.")]
        [SerializeField] private bool showGizmo = true;
        [Tooltip("미리보기에 쓸 카드 프리팹 (실제 카드는 Addressables로 로드되므로 에디터에선 여기 넣은 걸로 표시). 비우면 fallbackPrefab.")]
        [SerializeField] private GameObject gizmoPreviewPrefab;
        [Tooltip("미리보기로 쌓아 보여줄 카드 장수 (실제 카드 프리팹 모양으로 표시).")]
        [SerializeField, Range(0, 40)] private int gizmoPreviewCards = 6;
        [Tooltip("미리보기 흐트러짐 모양을 바꾸려면 숫자 변경.")]
        [SerializeField] private int gizmoSeed = 7;
        [Tooltip("fallbackPrefab이 없을 때 그릴 카드 크기 (가로, 세로 m).")]
        [SerializeField] private Vector2 gizmoCardSize = new Vector2(0.063f, 0.088f);
        [SerializeField] private Color gizmoColor = new Color(1f, 0.78f, 0.3f, 1f);

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

        // =====================================================================
        //  Editor Gizmo — 씬 뷰에서 더미 위치/크기를 바로 보고 옮길 수 있게
        //   · 항상: 흐트러짐 범위 원, 클릭 영역 박스, 미리보기 카드 (반투명)
        //   · 선택 시: 더 진하게 + 던지기 시작 높이 + 이름 라벨
        //   · 미리보기 카드/박스를 씬 뷰에서 클릭하면 이 오브젝트가 선택됨 → 이동 툴(W)로 옮기면 됨
        // =====================================================================
#if UNITY_EDITOR
        private void OnDrawGizmos() => DrawPileGizmo(false);
        private void OnDrawGizmosSelected() => DrawPileGizmo(true);

        private void DrawPileGizmo(bool selected)
        {
            if (!showGizmo) return;
            var anchor = pileAnchor != null ? pileAnchor : transform;
            float alpha = selected ? 1f : 0.45f;
            var prevMatrix = Gizmos.matrix;
            var prevColor = Gizmos.color;

            // ---- 흐트러짐 범위 원 + 중심 십자 (pileAnchor 로컬 XZ 평면) ----
            Gizmos.matrix = anchor.localToWorldMatrix;
            Gizmos.color = WithAlpha(gizmoColor, alpha);
            DrawCircleXZ(Vector3.up * baseYOffset, Mathf.Max(scatterRadius, 0.001f), 48);
            float cross = Mathf.Max(scatterRadius, gizmoCardSize.x * 0.5f);
            Gizmos.DrawLine(new Vector3(-cross, baseYOffset, 0f), new Vector3(cross, baseYOffset, 0f));
            Gizmos.DrawLine(new Vector3(0f, baseYOffset, -cross), new Vector3(0f, baseYOffset, cross));

            // ---- 미리보기 카드 (실제 위치 계산식과 동일) ----
            if (!Application.isPlaying || _spawned.Count == 0)
            {
                var rng = new System.Random(gizmoSeed);
                for (int i = 0; i < gizmoPreviewCards; i++)
                {
                    float a = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                    float d = Mathf.Sqrt((float)rng.NextDouble()) * scatterRadius;
                    var pos = new Vector3(Mathf.Cos(a) * d, baseYOffset + i * stackHeightPerCard, Mathf.Sin(a) * d);
                    float ang = Mathf.Lerp(-scatterAngle, scatterAngle, (float)rng.NextDouble());
                    bool top = i == gizmoPreviewCards - 1;
                    DrawPreviewCard(anchor, pos, ang, WithAlpha(gizmoColor, (top ? 0.55f : 0.25f) * (selected ? 1f : 0.7f)), top && selected);
                }
            }

            // ---- 던지기 시작 높이 (선택 시) ----
            if (selected && animateToss)
            {
                Gizmos.matrix = anchor.localToWorldMatrix;
                Gizmos.color = WithAlpha(gizmoColor, 0.6f);
                float topY = baseYOffset + Mathf.Max(0, gizmoPreviewCards - 1) * stackHeightPerCard;
                Gizmos.DrawLine(new Vector3(0f, topY, 0f), new Vector3(0f, topY + tossHeight, 0f));
                DrawCircleXZ(new Vector3(0f, topY + tossHeight, 0f), Mathf.Max(scatterRadius * 0.5f, 0.005f), 24);
            }

            // ---- 클릭 영역 (자동 콜라이더와 같은 크기, 이 오브젝트 기준) ----
            if (autoCollider)
            {
                int n = Application.isPlaying ? _spawned.Count : gizmoPreviewCards;
                float h = Mathf.Max(colliderSize.y, baseYOffset + n * stackHeightPerCard + 0.02f);
                Gizmos.matrix = transform.localToWorldMatrix;
                var size = new Vector3(colliderSize.x, h, colliderSize.z);
                var center = new Vector3(0f, h * 0.5f, 0f);
                Gizmos.color = WithAlpha(gizmoColor, selected ? 0.08f : 0.03f);
                Gizmos.DrawCube(center, size); // 씬 뷰에서 클릭해 선택 가능
                Gizmos.color = WithAlpha(gizmoColor, selected ? 0.9f : 0.35f);
                Gizmos.DrawWireCube(center, size);
            }

            Gizmos.matrix = prevMatrix;
            Gizmos.color = prevColor;

            if (selected)
            {
                var style = new GUIStyle(UnityEditor.EditorStyles.boldLabel) { normal = { textColor = gizmoColor } };
                UnityEditor.Handles.Label(anchor.position + anchor.up * (baseYOffset + colliderSize.y + 0.03f),
                    $"중앙 버림패 더미\n흐트러짐 반경 {scatterRadius:0.###}m · 최대 {maxVisibleCards}장", style);
            }
        }

        private void DrawPreviewCard(Transform anchor, Vector3 localPos, float angle, Color color, bool outline)
        {
            var prefab = gizmoPreviewPrefab != null ? gizmoPreviewPrefab : fallbackPrefab;
            Quaternion baseRot = Quaternion.Euler(additionalRotation);
            Vector3 scale = Vector3.one * scaleMultiplier;
            if (prefab != null)
            {
                if (preservePrefabRotation) baseRot = prefab.transform.localRotation * baseRot;
                if (preservePrefabScale) scale = prefab.transform.localScale * scaleMultiplier;
            }
            Quaternion rot = Quaternion.AngleAxis(angle, scatterAxis.sqrMagnitude > 0f ? scatterAxis.normalized : Vector3.up) * baseRot;
            Matrix4x4 cardMatrix = anchor.localToWorldMatrix * Matrix4x4.TRS(localPos, rot, scale);

            bool drew = false;
            if (prefab != null)
            {
                var rootInv = prefab.transform.worldToLocalMatrix;
                foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh == null) continue;
                    Gizmos.matrix = cardMatrix * rootInv * mf.transform.localToWorldMatrix;
                    Gizmos.color = color;
                    Gizmos.DrawMesh(mf.sharedMesh);
                    if (outline)
                    {
                        Gizmos.color = WithAlpha(color, 1f);
                        var b = mf.sharedMesh.bounds;
                        Gizmos.DrawWireCube(b.center, b.size);
                    }
                    drew = true;
                }
            }

            if (!drew) // 프리팹이 없으면 기본 카드 크기의 얇은 판
            {
                Gizmos.matrix = anchor.localToWorldMatrix * Matrix4x4.TRS(localPos, Quaternion.AngleAxis(angle, Vector3.up), Vector3.one);
                var size = new Vector3(gizmoCardSize.x, 0.0005f, gizmoCardSize.y);
                Gizmos.color = color;
                Gizmos.DrawCube(Vector3.zero, size);
                Gizmos.color = WithAlpha(color, outline ? 1f : 0.6f);
                Gizmos.DrawWireCube(Vector3.zero, size);
            }
        }

        private static void DrawCircleXZ(Vector3 center, float radius, int segments)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
#endif
    }
}
