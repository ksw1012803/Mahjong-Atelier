using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 후로 면자 시각 표시.
    /// 
    /// 한 플레이어의 후로 면자를 가로로 배열 표시. 각 면자는 3~4장.
    /// 
    /// 시각 룰 (단순화):
    ///   - 모든 면자 가로 배치 (회전 정확도는 추후 디자인 폴리시에)
    ///   - 안깡: 양 끝 패 뒷면 (TileSprite를 BackSprite로) - 본 구현은 단순화하여 앞면 표시
    ///   - 가져온 패는 90도 회전 (가로 누이기)
    /// 
    /// 본 구현은 가장 단순: 면자 각 패를 그냥 가로로 배치.
    /// 추후 폴리시 시 회전 및 출처 시각화 추가.
    /// </summary>
    public class CalledMeldsView : MonoBehaviour
    {
        [Header("Target Player")]
        [Tooltip("이 영역이 표시할 플레이어 인덱스")]
        [SerializeField] private int playerIndex = 0;

        [Header("Layout")]
        [SerializeField] private RectTransform meldsContainer;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private MahjongTileDatabase tileDatabase;

        [Header("Spacing")]
        [SerializeField] private float tileSpacing = 50f;
        [SerializeField] private float meldGap = 20f;

        [Header("Rotation Override (optional)")]
        [Tooltip("강제 회전 각도. 0이 아니면 사용. 음수=시계방향")]
        [SerializeField] private float forcedRotation = 0f;

        [Header("Game Manager")]
        [SerializeField] private MahjongGameManager gameManager;

        private readonly List<GameObject> _spawnedTiles = new List<GameObject>();
        private SeatPosition _myPosition;
        private float _tileRotation;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindObjectOfType<MahjongGameManager>();
        }

        private void OnEnable()
        {
            GameEvents.OnCallDeclared += HandleCallDeclared;
            GameEvents.OnHandChanged += HandleHandChanged;
            GameEvents.OnDealCompleted += HandleDealCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OnCallDeclared -= HandleCallDeclared;
            GameEvents.OnHandChanged -= HandleHandChanged;
            GameEvents.OnDealCompleted -= HandleDealCompleted;
        }

        private void Start()
        {
            if (gameManager != null && gameManager.Players.Count > 0)
            {
                _myPosition = SeatPositionHelper.GetPositionFor(
                    playerIndex, gameManager.UserPlayerIndex, gameManager.Players.Count);
                _tileRotation = forcedRotation != 0f ? forcedRotation : SeatPositionHelper.GetTileRotation(_myPosition);
            }
            UpdateDisplay();
        }

        private void HandleCallDeclared(int idx, CalledMeld meld)
        {
            if (idx == playerIndex) UpdateDisplay();
        }

        private void HandleHandChanged(int idx)
        {
            if (idx == playerIndex) UpdateDisplay();
        }

        private void HandleDealCompleted() => UpdateDisplay();

        private void UpdateDisplay()
        {
            ClearTiles();
            if (gameManager == null || gameManager.Players.Count <= playerIndex) return;

            var player = gameManager.Players[playerIndex];
            if (player.CalledMeldsExt.Count == 0) return;

            float cursor = 0f;
            foreach (var meld in player.CalledMeldsExt)
            {
                for (int i = 0; i < meld.Tiles.Count; i++)
                {
                    SpawnMeldTile(meld.Tiles[i], cursor);
                    cursor += tileSpacing;
                }
                cursor += meldGap;
            }
        }

        private void SpawnMeldTile(TileData tileData, float xOffset)
        {
            if (tilePrefab == null || meldsContainer == null || tileData == null) return;

            var go = Instantiate(tilePrefab, meldsContainer);
            go.SetActive(true);
            _spawnedTiles.Add(go);

            // 상호작용 비활성
            var dragUI = go.GetComponent<TileDragUI>();
            if (dragUI != null) dragUI.enabled = false;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            var view = go.GetComponent<TileView>();
            if (view != null)
            {
                var sprite = tileDatabase != null ? tileDatabase.GetSprite(tileData) : null;
                view.SetTile(tileData, sprite);
            }

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.localPosition = ComputePosition(xOffset);
                rt.localRotation = Quaternion.Euler(0, 0, _tileRotation);
            }
        }

        private Vector3 ComputePosition(float offset)
        {
            // 좌석 방향에 따라 가로/세로 배치
            switch (_myPosition)
            {
                case SeatPosition.Self:
                case SeatPosition.Toimen:
                    return new Vector3(offset, 0, 0);
                case SeatPosition.ShimoCha:
                case SeatPosition.KamiCha:
                    return new Vector3(0, -offset, 0);
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
