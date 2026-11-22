using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 한 플레이어의 버림패(河) 표시.
    /// 
    /// 표준 마작 화면 룰: 6열로 배열, 7번째 패부터 둘째 줄로.
    /// 본 구현: 6열 × N행 그리드.
    /// 
    /// 회전: 손패와 동일 (각 좌석에 맞춰).
    /// </summary>
    public class DiscardPileView : MonoBehaviour
    {
        [Header("Target Player")]
        [SerializeField] private int playerIndex = 0;

        [Header("Layout")]
        [SerializeField] private RectTransform tilesContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private MahjongTileDatabase tileDatabase;

        [Header("Grid")]
        [Tooltip("한 행당 타일 수 (표준 6)")]
        [SerializeField] private int tilesPerRow = 6;

        [Tooltip("타일 가로 간격")]
        [SerializeField] private float horizontalSpacing = 50f;

        [Tooltip("행 사이 세로 간격")]
        [SerializeField] private float verticalSpacing = 70f;

        [Header("Game Manager")]
        [SerializeField] private MahjongGameManager gameManager;

        private SeatPosition _myPosition;
        private float _tileRotation;
        private readonly List<GameObject> _spawnedTiles = new List<GameObject>();

        private void Awake()
        {
            if (gameManager == null)
                gameManager = FindObjectOfType<MahjongGameManager>();
        }

        private void OnEnable()
        {
            GameEvents.OnTileDiscarded += HandleTileDiscarded;
            GameEvents.OnDealCompleted += HandleDealCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OnTileDiscarded -= HandleTileDiscarded;
            GameEvents.OnDealCompleted -= HandleDealCompleted;
        }

        private void Start()
        {
            if (gameManager != null && gameManager.Players.Count > 0)
            {
                _myPosition = SeatPositionHelper.GetPositionFor(
                    playerIndex, gameManager.UserPlayerIndex, gameManager.Players.Count);
                _tileRotation = SeatPositionHelper.GetTileRotation(_myPosition);
            }
            UpdateDisplay();
        }

        private void HandleDealCompleted() => UpdateDisplay();

        private void HandleTileDiscarded(int idx, TileData tile)
        {
            if (idx == playerIndex) UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            ClearTiles();
            if (gameManager == null || gameManager.Players.Count <= playerIndex) return;

            var player = gameManager.Players[playerIndex];
            var discards = player.DiscardPile;
            for (int i = 0; i < discards.Count; i++)
            {
                SpawnDiscardTile(discards[i], i);
            }
        }

        private void SpawnDiscardTile(TileData tileData, int index)
        {
            if (tilePrefab == null || tilesContainer == null) return;

            var go = Instantiate(tilePrefab, tilesContainer);
            go.SetActive(true);
            _spawnedTiles.Add(go);

            // 상호작용 비활성
            var dragUI = go.GetComponent<TileDragUI>();
            if (dragUI != null) dragUI.enabled = false;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            // 패 정보 설정
            var view = go.GetComponent<TileView>();
            if (view != null)
            {
                var sprite = tileDatabase != null ? tileDatabase.GetSprite(tileData) : null;
                view.SetTile(tileData, sprite);
            }

            // 위치 + 회전
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localPosition = ComputeGridPosition(index);
                rt.localRotation = Quaternion.Euler(0, 0, _tileRotation);
            }
        }

        /// <summary>
        /// 인덱스 i번 패를 그리드 (col, row)에 배치.
        /// 회전에 따라 그리드 방향이 다름.
        /// </summary>
        private Vector3 ComputeGridPosition(int index)
        {
            int col = index % tilesPerRow;
            int row = index / tilesPerRow;

            // 그리드 중심을 0으로 (가로 중앙 정렬)
            float xOffset = (col - (tilesPerRow - 1) * 0.5f) * horizontalSpacing;
            float yOffset = -row * verticalSpacing; // 아래로 갈수록 -y

            switch (_myPosition)
            {
                case SeatPosition.Self:
                    return new Vector3(xOffset, yOffset, 0);
                case SeatPosition.Toimen:
                    // 위쪽: x 반전 (180도 회전에 맞춤), y 반전
                    return new Vector3(-xOffset, -yOffset, 0);
                case SeatPosition.ShimoCha:
                    // 오른쪽: 가로/세로 축 교환
                    return new Vector3(-yOffset, -xOffset, 0);
                case SeatPosition.KamiCha:
                    // 왼쪽: 가로/세로 축 교환 (반대)
                    return new Vector3(yOffset, xOffset, 0);
                default:
                    return new Vector3(xOffset, yOffset, 0);
            }
        }

        private void ClearTiles()
        {
            foreach (var go in _spawnedTiles)
                if (go != null) Destroy(go);
            _spawnedTiles.Clear();
        }
    }
}
