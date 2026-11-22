using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 상대(봇) 손패 표시. 13장 (또는 14장 쯔모 시) 뒷면 타일로.
    /// 
    /// 회전:
    ///   - ShimoCha (오른쪽): -90도
    ///   - Toimen (위): 180도
    ///   - KamiCha (왼쪽): 90도
    /// 
    /// 본 컴포넌트는 화면 한 영역 (P1/P2/P3 중 하나)을 담당.
    /// Inspector에서 PlayerIndex 설정 → 해당 플레이어의 손패 자동 추적.
    /// </summary>
    public class OpponentHandView : MonoBehaviour
    {
        [Header("Target Player")]
        [Tooltip("이 영역이 보여줄 플레이어 인덱스 (1, 2, 3)")]
        [SerializeField] private int playerIndex = 1;

        [Header("Layout")]
        [Tooltip("타일이 들어갈 부모 컨테이너")]
        [SerializeField] private RectTransform tilesContainer;

        [Tooltip("뒷면 타일 프리팹 (Image 컴포넌트 포함)")]
        [SerializeField] private GameObject backTilePrefab;

        [Header("Spacing")]
        [Tooltip("타일 사이 간격 (회전에 따라 X 또는 Y 적용)")]
        [SerializeField] private float tileSpacing = 30f;

        [Header("Game Manager (선택, 자동 검색)")]
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
            GameEvents.OnHandChanged += HandleHandChanged;
            GameEvents.OnDealCompleted += HandleDealCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OnHandChanged -= HandleHandChanged;
            GameEvents.OnDealCompleted -= HandleDealCompleted;
        }

        private void Start()
        {
            // 위치 자동 계산 + 회전 적용
            if (gameManager != null && gameManager.Players.Count >= 4)
            {
                _myPosition = SeatPositionHelper.GetPositionFor(
                    playerIndex, gameManager.UserPlayerIndex, gameManager.Players.Count);
                _tileRotation = SeatPositionHelper.GetTileRotation(_myPosition);
            }
            UpdateDisplay();
        }

        private void HandleDealCompleted() => UpdateDisplay();

        private void HandleHandChanged(int changedPlayerIndex)
        {
            if (changedPlayerIndex == playerIndex)
                UpdateDisplay();
        }

        // ====================================================
        // 디스플레이
        // ====================================================

        private void UpdateDisplay()
        {
            ClearTiles();
            if (gameManager == null || gameManager.Players.Count <= playerIndex) return;

            var player = gameManager.Players[playerIndex];
            int tileCount = player.HandTiles.Count;
            if (tileCount == 0) return;

            for (int i = 0; i < tileCount; i++)
            {
                SpawnBackTile(i, tileCount);
            }
        }

        private void SpawnBackTile(int index, int totalCount)
        {
            if (backTilePrefab == null || tilesContainer == null) return;

            var go = Instantiate(backTilePrefab, tilesContainer);
            go.SetActive(true);
            _spawnedTiles.Add(go);

            // 위치 + 회전 설정
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localPosition = ComputeTilePosition(index, totalCount);
                rt.localRotation = Quaternion.Euler(0, 0, _tileRotation);
            }
        }

        /// <summary>
        /// 타일 i번의 위치 계산. 회전 방향에 따라 X 또는 Y로 배치.
        /// </summary>
        private Vector3 ComputeTilePosition(int index, int totalCount)
        {
            float offset = (index - (totalCount - 1) * 0.5f) * tileSpacing;

            switch (_myPosition)
            {
                case SeatPosition.Toimen:
                    // 위쪽: 가로 배치, 왼쪽이 P2 입장에서 오른쪽 (180도)
                    return new Vector3(-offset, 0, 0);
                case SeatPosition.ShimoCha:
                    // 오른쪽: 세로 배치, 위→아래
                    return new Vector3(0, -offset, 0);
                case SeatPosition.KamiCha:
                    // 왼쪽: 세로 배치, 아래→위
                    return new Vector3(0, offset, 0);
                default:
                    return new Vector3(offset, 0, 0);
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
