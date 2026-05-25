using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;  // ← Input System 사용
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 손패 UI 매니저 (Unity 6 Input System 버전).
    /// 
    /// 변경점:
    ///   - GameManager 직접 참조 대신 GameEvents 구독
    ///   - 자동 정렬(F1 또는 버튼) 추가 — PPT에서 언급된 기능
    ///   - 쯔모 패는 정렬에서 제외하고 끝에 오프셋으로 표시
    /// 
    /// Input System:
    ///   - autoSortKey는 Keyboard.current를 통해 확인
    ///   - Key enum (UnityEngine.InputSystem.Key)을 인스펙터에서 선택
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
        [Tooltip("쯔모 패를 일반 패에서 떨어뜨리는 가로 간격")]
        public float tsumoOffset = 80f;

        [Header("Game Manager")]
        public MahjongGameManager gameManager;

        [Header("Auto Sort (Input System)")]
        [Tooltip("자동 정렬 단축키 (Unity 6 Input System)")]
        [SerializeField] private Key autoSortKey = Key.F1;
        [SerializeField] private bool autoSortOnDraw = false; // 쯔모 시 자동 정렬

        /// <summary>지금 손패에서 어떤 게 쯔모 패인지 (마지막에 추가된 것).</summary>
        private TileData _tsumoTile;

        private int previewIndex = -1;
        private TileDragUI previewDraggedTile = null;

        private void OnEnable()
        {
            // 어떤 플레이어 인덱스를 표시할지는 GameManager의 currentPlayerIndex를 따름.
            // 1인용 단계에서는 0번 플레이어만 있으므로 모든 이벤트를 수신.
            GameEvents.OnTileDrawn += HandleTileDrawn;
            GameEvents.OnHandChanged += HandleHandChanged;
            GameEvents.OnDealCompleted += HandleDealCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OnTileDrawn -= HandleTileDrawn;
            GameEvents.OnHandChanged -= HandleHandChanged;
            GameEvents.OnDealCompleted -= HandleDealCompleted;
        }

        private void Start()
        {
            SnapAllImmediately();
        }

        private void Update()
        {
            // Unity 6 Input System: Keyboard.current로 접근
            // null 체크 — 키보드 미연결 환경(모바일 등)에서도 안전
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard[autoSortKey].wasPressedThisFrame)
            {
                AutoSort();
            }
        }

        // ====================================================
        // 이벤트 핸들러
        // ====================================================

        private void HandleDealCompleted()
        {
            if (gameManager == null || gameManager.Players.Count == 0) return;
            var hand = gameManager.Players[0].HandTiles;
            _tsumoTile = null;
            BuildHand(hand);
            AutoSort(); // 배패 직후 자동 정렬
        }

        private void HandleTileDrawn(int playerIndex, TileData drawnTile)
        {
            if (playerIndex != 0) return; // 현재 1인용
            _tsumoTile = drawnTile;
            if (autoSortOnDraw)
            {
                // 정렬 후 쯔모 패는 끝에 오프셋 (HandSorter는 쯔모 구분 없이 정렬하므로 강제 처리)
                AutoSort(keepTsumoLast: true);
            }
            else
            {
                // 단순히 끝에 추가
                if (gameManager != null && gameManager.Players.Count > 0)
                    BuildHand(gameManager.Players[0].HandTiles);
            }
        }

        private void HandleHandChanged(int playerIndex)
        {
            // 단순 변경 알림. 필요 시 더 미세하게 처리.
        }

        // ====================================================
        // 자동 정렬
        // ====================================================

        /// <summary>
        /// 손패 자동 정렬. 표준 일본 마작 순서.
        /// keepTsumoLast=true이면 쯔모 패를 정렬에서 분리해 끝에 둠.
        /// </summary>
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
        // 기존 UI 기능 (드래그/프리뷰/스냅)
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
            if (debugLog) Debug.Log($"[HandUI] BeginPreview: {draggedTile.name}, idx={previewIndex}");
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
        // 타일 생성/파괴
        // ====================================================

        /// <summary>리스트 그대로 UI를 다시 빌드.</summary>
        public void BuildHand(List<TileData> handData)
        {
            ClearTiles();
            foreach (var tileData in handData)
                CreateTile(tileData);
            SnapAllImmediately();
        }

        // 기존 호환성을 위해 남겨둠 (다른 코드가 호출 중일 수 있음)
        public void CreateHand(List<TileData> handData) => BuildHand(handData);

        private void CreateTile(TileData tileData)
        {

            // === 디버그: 사전 상태 확인 ===
            //Debug.Log($"[CreateTile 시작] {tileData} | " +
            //          $"프리팹 activeSelf={tilePrefab.activeSelf}, " +
            //          $"tileRoot.activeSelf={tileRoot.gameObject.activeSelf}, " +
            //          $"tileRoot.activeInHierarchy={tileRoot.gameObject.activeInHierarchy}");

            GameObject obj = Instantiate(tilePrefab, tileRoot);
            //Debug.Log($"[1] Instantiate 직후: " +
            //          $"activeSelf={obj.activeSelf}, " +
            //          $"activeInHierarchy={obj.activeInHierarchy}, " +
            //          $"parent={obj.transform.parent?.name}");

            obj.SetActive(true);
            //Debug.Log($"[2] SetActive(true) 직후: " +
            //          $"activeSelf={obj.activeSelf}, " +
            //          $"activeInHierarchy={obj.activeInHierarchy}");

            var tileView = obj.GetComponent<TileView>();
            var dragUI = obj.GetComponent<TileDragUI>();

            Sprite sprite = tileDatabase.GetSprite(tileData);
            tileView.SetTile(tileData, sprite);
            //Debug.Log($"[3] SetTile 후: " +
            //          $"activeSelf={obj.activeSelf}, " +
            //          $"sprite={(sprite != null ? sprite.name : "null")}");

            dragUI.handManager = this;
            dragUI.canvas = GetComponentInParent<Canvas>();
            dragUI.handArea = GetComponent<RectTransform>();

            tiles.Add(dragUI);
            //Debug.Log($"[4] tiles.Add 후: " +
            //          $"activeSelf={obj.activeSelf}, " +
            //          $"activeInHierarchy={obj.activeInHierarchy}");
            //Debug.Log($"[CreateTile] {tileData} 생성 후 activeSelf={obj.activeSelf}, activeInHierarchy={obj.activeInHierarchy}");
        }

        public void ClearTiles()
        {
            foreach (var t in tiles)
                if (t != null) Destroy(t.gameObject);
            tiles.Clear();
        }

        // ====================================================
        // 슬롯 위치 계산
        // ====================================================

        private Vector2 GetSlotPosition(int index)
        {
            if (index < 0 || index >= slots.Count)
            {
                Debug.LogError($"슬롯 인덱스 초과: {index}");
                return Vector2.zero;
            }

            Vector2 pos = slots[index].anchoredPosition;

            // 마지막 패가 쯔모 패라면 오프셋
            if (tiles.Count == 14 && index == tiles.Count - 1)
            {
                pos.x += tsumoOffset;
            }

            return pos;
        }

        // ====================================================
        // 클릭 처리: 게임매니저에 버림 요청
        // ====================================================

        public void OnTileClicked(TileDragUI tile)
        {
            var tileView = tile.GetComponent<TileView>();
            if (tileView == null || gameManager == null) return;
            gameManager.RequestDiscard(tileView.TileData);
        }
    }
}