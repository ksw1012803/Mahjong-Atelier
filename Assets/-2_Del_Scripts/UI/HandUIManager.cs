using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 손패 UI 매니저 v5 — 사용자 정렬 순서 보존.
    /// 
    /// 변경점:
    ///   - 손패 변경 시 통째로 다시 그리지 않고 변경분만 반영
    ///   - 사용자가 드래그로 정렬한 순서 유지
    ///   - 후로/버림 시 빠진 패만 제거, 쯔모 시 새 패만 추가
    /// </summary>
    public class HandUIManager : MonoBehaviour
    {
        [Header("Slots")]
        public List<RectTransform> slots = new List<RectTransform>();

        [Header("Tiles")]
        public List<TileDragUI> tiles = new List<TileDragUI>();

        [Header("Debug")]
        public bool debugLog = false;

        [Header("Tile Spawn")]
        public Transform tileRoot;
        public GameObject tilePrefab;
        public MahjongTileDatabase tileDatabase;

        [Header("Tsumo Tile")]
        public float tsumoOffset = 80f;

        [Header("Game Manager")]
        public MahjongGameManager gameManager;

        [Header("Auto Sort (Input System)")]
        [SerializeField] private Key autoSortKey = Key.F1;
        [SerializeField] private bool autoSortOnDraw = false;

        [Header("Riichi Integration")]
        [SerializeField] private RiichiButton riichiButton;

        [Header("Highlight Style")]
        [Range(0.2f, 1f)]
        [SerializeField] private float dimmedAlpha = 0.4f;

        private TileData _tsumoTile;
        private int previewIndex = -1;
        private TileDragUI previewDraggedTile = null;
        private HashSet<TileData> _highlightFilter = null;

        private bool _needsSync = false;

        private void OnEnable()
        {
            GameEvents.OnTileDrawn += HandleTileDrawn;
            GameEvents.OnHandChanged += HandleHandChanged;
            GameEvents.OnDealCompleted += HandleDealCompleted;
            GameEvents.OnCallDeclared += HandleCallDeclared;
        }

        private void OnDisable()
        {
            GameEvents.OnTileDrawn -= HandleTileDrawn;
            GameEvents.OnHandChanged -= HandleHandChanged;
            GameEvents.OnDealCompleted -= HandleDealCompleted;
            GameEvents.OnCallDeclared -= HandleCallDeclared;
        }

        private void Start()
        {
            SnapAllImmediately();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[autoSortKey].wasPressedThisFrame)
            {
                AutoSort();
            }

            // 지연 동기화: 사용자 순서 유지하면서 추가/제거만 반영
            if (_needsSync)
            {
                _needsSync = false;
                SyncWithPlayerHand();
            }
        }

        // ====================================================
        // 이벤트 핸들러
        // ====================================================

        private void HandleDealCompleted()
        {
            // 배패 시작 시는 전체 재구성 (사용자 정렬 보존할 게 없음)
            if (gameManager == null || gameManager.Players.Count == 0) return;
            var hand = gameManager.Players[0].HandTiles;
            _tsumoTile = null;
            BuildHand(hand);
            AutoSort();
        }

        private void HandleTileDrawn(int playerIndex, TileData drawnTile)
        {
            if (playerIndex != 0) return;
            _tsumoTile = drawnTile;
            // 쯔모 패는 SyncWithPlayerHand에서 끝에 추가됨 (이벤트 OnHandChanged도 함께 옴)
            // autoSortOnDraw가 켜져있으면 정렬, 기본은 사용자 순서 유지
        }

        private void HandleHandChanged(int playerIndex)
        {
            if (playerIndex != 0) return;
            _needsSync = true;
        }

        private void HandleCallDeclared(int playerIndex, CalledMeld meld)
        {
            if (playerIndex != 0) return;
            _tsumoTile = null; // 후로 시 쯔모 표시 제거
            _needsSync = true;
        }

        // ====================================================
        // 동기화 — 핵심
        // ====================================================

        /// <summary>
        /// 플레이어 손패와 UI를 동기화. 사용자 정렬 순서 보존.
        /// 
        /// 동작:
        ///   1) UI에 있는데 데이터에 없는 패 → UI에서 제거 (정상 자리 유지)
        ///   2) 데이터에 있는데 UI에 없는 패 → UI 끝에 추가 (쯔모 패)
        ///   3) 양쪽에 다 있는 패 → 그대로 (위치 유지)
        /// </summary>
        private void SyncWithPlayerHand()
        {
            if (gameManager == null || gameManager.Players.Count == 0) return;

            var playerHand = gameManager.Players[0].HandTiles;
            var playerSet = new HashSet<TileData>(playerHand);

            // 1) UI에서 데이터에 없는 패 제거
            for (int i = tiles.Count - 1; i >= 0; i--)
            {
                var dragUI = tiles[i];
                if (dragUI == null)
                {
                    tiles.RemoveAt(i);
                    continue;
                }
                var view = dragUI.GetComponent<TileView>();
                if (view == null || !playerSet.Contains(view.TileData))
                {
                    Destroy(dragUI.gameObject);
                    tiles.RemoveAt(i);
                }
            }

            // 2) 데이터에 있는데 UI에 없는 패 추가
            var uiSet = new HashSet<TileData>();
            foreach (var t in tiles)
            {
                if (t == null) continue;
                var view = t.GetComponent<TileView>();
                if (view != null) uiSet.Add(view.TileData);
            }

            foreach (var tileData in playerHand)
            {
                if (!uiSet.Contains(tileData))
                {
                    CreateTile(tileData);
                }
            }

            // 3) 슬롯 인덱스 재할당 + 위치 갱신
            SnapAllImmediately();

            // 강조 필터가 활성이면 다시 적용
            if (_highlightFilter != null)
                ApplyHighlightToAll();
        }

        // ====================================================
        // 강조 필터 (리치 모드)
        // ====================================================

        public void SetTileHighlightFilter(List<TileData> allowedTiles)
        {
            _highlightFilter = new HashSet<TileData>();
            foreach (var t in allowedTiles) _highlightFilter.Add(t);
            ApplyHighlightToAll();
        }

        public void ClearTileHighlightFilter()
        {
            _highlightFilter = null;
            ApplyHighlightToAll();
        }

        private void ApplyHighlightToAll()
        {
            foreach (var dragUI in tiles)
            {
                if (dragUI == null) continue;
                var view = dragUI.GetComponent<TileView>();
                if (view == null) continue;

                bool isHighlighted = _highlightFilter == null
                    || _highlightFilter.Contains(view.TileData);
                ApplyHighlight(view, isHighlighted);
            }
        }

        private void ApplyHighlight(TileView view, bool highlighted)
        {
            if (view == null || view.tileImage == null) return;
            var c = view.tileImage.color;
            c.a = highlighted ? 1f : dimmedAlpha;
            view.tileImage.color = c;
        }

        // ====================================================
        // 자동 정렬 (F1 키)
        // ====================================================

        public void AutoSort(bool keepTsumoLast = true)
        {
            if (gameManager == null || gameManager.Players.Count == 0) return;
            var handData = new List<TileData>(gameManager.Players[0].HandTiles);

            TileData tsumoSeparated = null;
            if (keepTsumoLast && _tsumoTile != null && handData.Contains(_tsumoTile))
            {
                tsumoSeparated = _tsumoTile;
                handData.Remove(tsumoSeparated);
            }

            var sorted = HandSorter.Sorted(handData);
            if (tsumoSeparated != null) sorted.Add(tsumoSeparated);

            BuildHand(sorted);
        }

        // ====================================================
        // 드래그/스냅
        // ====================================================

        public void SnapAllImmediately()
        {
            int count = Mathf.Min(tiles.Count, slots.Count);
            for (int i = 0; i < count; i++)
            {
                tiles[i].SetSlotIndex(i);
                tiles[i].ForceSetPosition(GetSlotPosition(i));
            }
        }

        public void SnapAllSmooth()
        {
            int count = Mathf.Min(tiles.Count, slots.Count);
            for (int i = 0; i < count; i++)
            {
                tiles[i].SetSlotIndex(i);
                tiles[i].MoveToSlot(GetSlotPosition(i));
            }
        }

        public int GetNearestSlotIndex(Vector2 tilePos)
        {
            int nearestIndex = 0;
            float minDist = float.MaxValue;
            for (int i = 0; i < slots.Count; i++)
            {
                float dist = Mathf.Abs(tilePos.x - GetSlotPosition(i).x);
                if (dist < minDist) { minDist = dist; nearestIndex = i; }
            }
            return nearestIndex;
        }

        public void BeginPreview(TileDragUI draggedTile)
        {
            previewDraggedTile = draggedTile;
            previewIndex = tiles.IndexOf(draggedTile);
        }

        public void PreviewInsert(TileDragUI draggedTile)
        {
            if (draggedTile == null || tiles.Count == 0 || slots.Count == 0) return;
            int targetIndex = GetNearestSlotIndex(draggedTile.GetCurrentPos());
            if (previewDraggedTile == draggedTile && previewIndex == targetIndex) return;

            previewDraggedTile = draggedTile;
            previewIndex = targetIndex;

            var previewOrder = new List<TileDragUI>(tiles);
            previewOrder.Remove(draggedTile);
            if (targetIndex > previewOrder.Count) targetIndex = previewOrder.Count;
            previewOrder.Insert(targetIndex, draggedTile);

            for (int i = 0; i < previewOrder.Count; i++)
            {
                var tile = previewOrder[i];
                if (tile == draggedTile) continue;
                tile.SetSlotIndex(i);
                tile.MoveToSlot(GetSlotPosition(i));
            }
        }

        public void CommitInsert(TileDragUI draggedTile)
        {
            if (draggedTile == null || tiles.Count == 0 || slots.Count == 0) return;
            int oldIndex = tiles.IndexOf(draggedTile);
            if (oldIndex < 0) return;
            int targetIndex = GetNearestSlotIndex(draggedTile.GetCurrentPos());
            tiles.RemoveAt(oldIndex);
            if (targetIndex > tiles.Count) targetIndex = tiles.Count;
            tiles.Insert(targetIndex, draggedTile);
            SnapAllSmooth();
            previewDraggedTile = null;
            previewIndex = -1;
        }

        public void CancelPreview()
        {
            previewDraggedTile = null;
            previewIndex = -1;
            SnapAllSmooth();
        }

        // ====================================================
        // 타일 생성 (전체 재구성용 — 배패/자동정렬)
        // ====================================================

        public void BuildHand(List<TileData> handData)
        {
            ClearTiles();
            foreach (var tileData in handData)
                CreateTile(tileData);
            SnapAllImmediately();

            if (_highlightFilter != null)
                ApplyHighlightToAll();
        }

        public void CreateHand(List<TileData> handData) => BuildHand(handData);

        private void CreateTile(TileData tileData)
        {
            GameObject obj = Instantiate(tilePrefab, tileRoot);
            obj.SetActive(true);

            var tileView = obj.GetComponent<TileView>();
            var dragUI = obj.GetComponent<TileDragUI>();

            Sprite sprite = tileDatabase.GetSprite(tileData);
            tileView.SetTile(tileData, sprite);

            dragUI.handManager = this;
            dragUI.canvas = GetComponentInParent<Canvas>();
            dragUI.handArea = GetComponent<RectTransform>();

            tiles.Add(dragUI);
        }

        public void ClearTiles()
        {
            foreach (var t in tiles)
                if (t != null) Destroy(t.gameObject);
            tiles.Clear();
        }

        // ====================================================
        // 슬롯 위치
        // ====================================================

        private Vector2 GetSlotPosition(int index)
        {
            if (index < 0 || index >= slots.Count)
            {
                Debug.LogError($"슬롯 인덱스 초과: {index}");
                return Vector2.zero;
            }

            Vector2 pos = slots[index].anchoredPosition;

            if (tiles.Count == 14 && index == tiles.Count - 1)
                pos.x += tsumoOffset;

            return pos;
        }

        // ====================================================
        // 클릭 라우팅
        // ====================================================

        public void OnTileClicked(TileDragUI tile)
        {
            var tileView = tile.GetComponent<TileView>();
            if (tileView == null || gameManager == null) return;

            if (riichiButton != null && riichiButton.IsInRiichiSelectionMode)
            {
                if (riichiButton.TryHandleTileClick(tileView.TileData))
                    return;
            }

            gameManager.RequestDiscard(tileView.TileData);
        }
    }
}
