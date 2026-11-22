using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 유국 결과 다이얼로그 (수정판).
    /// 
    /// 변경점:
    ///   - DialogContainer 분리. 이 컴포넌트는 항상 활성, 자식만 토글.
    /// </summary>
    public class ExhaustiveDrawPanel : MonoBehaviour
    {
        [System.Serializable]
        public class PlayerEntry
        {
            public int playerIndex;
            public TMP_Text seatWindText;
            public TMP_Text statusText;
            public TMP_Text pointChangeText;
            public RectTransform waitsContainer;
            public GameObject waitsSection;
        }

        [Header("Dialog Container (자식 GameObject, 처음 비활성)")]
        [SerializeField] private GameObject dialogContainer;

        [Header("Title")]
        [SerializeField] private TMP_Text titleText;

        [Header("Player Entries (4명)")]
        [SerializeField] private PlayerEntry[] entries = new PlayerEntry[4];

        [Header("Dealer Repeat Info")]
        [SerializeField] private TMP_Text dealerRepeatText;

        [Header("Tile Display")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private MahjongTileDatabase tileDatabase;

        [Header("Buttons")]
        [SerializeField] private Button nextButton;

        public event Action OnNextButtonClicked;

        private readonly List<GameObject> _spawnedTiles = new List<GameObject>();

        private void Awake()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            if (nextButton != null) nextButton.onClick.AddListener(HandleNextClicked);
        }

        private void OnDestroy()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(HandleNextClicked);
        }

        public void Show(ExhaustiveDrawResult result)
        {
            if (result == null) return;
            ClearSpawnedTiles();

            if (titleText != null)
                titleText.text = "유국 (流局)";

            foreach (var entry in entries)
            {
                if (entry == null) continue;
                var status = FindStatusFor(result, entry.playerIndex);
                if (status == null) continue;
                UpdateEntry(entry, status);
            }

            if (dealerRepeatText != null)
            {
                dealerRepeatText.text = result.IsDealerRepeat
                    ? "친 연장"
                    : "친 이동";
            }

            if (dialogContainer != null) dialogContainer.SetActive(true);
        }

        public void Hide()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            ClearSpawnedTiles();
        }

        private ExhaustiveDrawResult.PlayerStatus FindStatusFor(
            ExhaustiveDrawResult result, int playerIndex)
        {
            foreach (var s in result.Statuses)
                if (s.PlayerIndex == playerIndex) return s;
            return null;
        }

        private void UpdateEntry(PlayerEntry entry, ExhaustiveDrawResult.PlayerStatus status)
        {
            if (entry.seatWindText != null)
                entry.seatWindText.text = SeatToKanji(status.Seat);

            if (entry.statusText != null)
                entry.statusText.text = status.IsTenpai ? "텐파이" : "노텐";

            if (entry.pointChangeText != null)
            {
                if (status.PointChange > 0)
                    entry.pointChangeText.text = $"+{status.PointChange}";
                else if (status.PointChange < 0)
                    entry.pointChangeText.text = $"{status.PointChange}";
                else
                    entry.pointChangeText.text = "—";
            }

            bool showWaits = status.IsTenpai && status.Waits != null && status.Waits.Count > 0;
            if (entry.waitsSection != null) entry.waitsSection.SetActive(showWaits);

            if (showWaits && entry.waitsContainer != null && tilePrefab != null)
            {
                foreach (var kind in status.Waits)
                    SpawnTileImage(kind, entry.waitsContainer);
            }
        }

        private void SpawnTileImage(TileKind kind, RectTransform parent)
        {
            var go = Instantiate(tilePrefab, parent);
            go.SetActive(true);
            _spawnedTiles.Add(go);

            var dragUI = go.GetComponent<TileDragUI>();
            if (dragUI != null) dragUI.enabled = false;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            var view = go.GetComponent<TileView>();
            if (view != null)
            {
                var tempData = new TileData(0, kind, false);
                var sprite = tileDatabase != null ? tileDatabase.GetSprite(tempData) : null;
                view.SetTile(tempData, sprite);
            }
        }

        private void ClearSpawnedTiles()
        {
            foreach (var go in _spawnedTiles)
                if (go != null) Destroy(go);
            _spawnedTiles.Clear();
        }

        private static string SeatToKanji(SeatWind seat)
        {
            switch (seat)
            {
                case SeatWind.East: return "東";
                case SeatWind.South: return "南";
                case SeatWind.West: return "西";
                case SeatWind.North: return "北";
                default: return "?";
            }
        }

        private void HandleNextClicked()
        {
            Hide();
            OnNextButtonClicked?.Invoke();
        }
    }
}
