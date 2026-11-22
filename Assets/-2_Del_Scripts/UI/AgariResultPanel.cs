using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 화료 결과 다이얼로그 (수정판).
    /// 
    /// 변경점:
    ///   - 이 컴포넌트는 **항상 활성** 상태로 유지해야 함 (게임 매니저가 호출하니까)
    ///   - 시각적으로 보이는 다이얼로그 본체는 자식 GameObject (DialogContainer)
    ///   - Show/Hide는 DialogContainer만 토글 (이 GameObject 자체는 그대로)
    /// 
    /// Inspector 구조 권장:
    ///   AgariResultPanel (이 컴포넌트 보유, 항상 활성)
    ///   └── DialogContainer (자식, 처음에 비활성 — Hide 상태)
    ///       ├── Background (어두운 반투명 전체 덮개)
    ///       ├── DialogBox (내용)
    ///       └── ...
    /// </summary>
    public class AgariResultPanel : MonoBehaviour
    {
        [Header("Dialog Container (자식 GameObject, 항상 비활성으로 시작)")]
        [Tooltip("실제 다이얼로그 본체. Show 시 활성화됨.")]
        [SerializeField] private GameObject dialogContainer;

        [Header("Hand Display")]
        [SerializeField] private RectTransform tileRowParent;
        [SerializeField] private RectTransform winningTileParent;
        [SerializeField] private RectTransform doraIndicatorParent;
        [SerializeField] private RectTransform uraDoraIndicatorParent;
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private MahjongTileDatabase tileDatabase;

        [Header("Yaku List")]
        [SerializeField] private RectTransform yakuListParent;
        [SerializeField] private GameObject yakuEntryPrefab;

        [Header("Score Display")]
        [SerializeField] private TMP_Text scoreClassText;
        [SerializeField] private TMP_Text fuHanText;
        [SerializeField] private TMP_Text totalGainText;
        [SerializeField] private TMP_Text paymentText;

        [Header("Section Headers")]
        [SerializeField] private GameObject uraDoraSection;

        [Header("Buttons")]
        [SerializeField] private Button nextButton;

        public event Action OnNextButtonClicked;

        private readonly List<GameObject> _spawnedItems = new List<GameObject>();

        private void Awake()
        {
            // Dialog는 처음엔 안 보이게
            if (dialogContainer != null) dialogContainer.SetActive(false);
            if (nextButton != null) nextButton.onClick.AddListener(HandleNextClicked);
        }

        private void OnDestroy()
        {
            if (nextButton != null) nextButton.onClick.RemoveListener(HandleNextClicked);
        }

        public void Show(AgariResultData data)
        {
            if (data == null) return;
            ClearSpawned();

            DisplayHandTiles(data);
            DisplayWinningTile(data);
            DisplayDoraIndicators(data);
            DisplayYakuList(data);
            DisplayScoreInfo(data);

            if (dialogContainer != null) dialogContainer.SetActive(true);
        }

        public void Hide()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            ClearSpawned();
        }

        // === 표시 로직 (이전과 동일) ===

        private void DisplayHandTiles(AgariResultData data)
        {
            if (tileRowParent == null) return;
            var sorted = new List<TileData>(data.HandTiles);
            if (data.WinningTile != null) sorted.Remove(data.WinningTile);
            var displayList = HandSorter.Sorted(sorted);
            foreach (var tile in displayList)
                SpawnTile(tile, tileRowParent);
        }

        private void DisplayWinningTile(AgariResultData data)
        {
            if (winningTileParent == null || data.WinningTile == null) return;
            SpawnTile(data.WinningTile, winningTileParent);
        }

        private void DisplayDoraIndicators(AgariResultData data)
        {
            if (doraIndicatorParent != null)
            {
                foreach (var ind in data.DoraIndicators)
                    SpawnTile(ind, doraIndicatorParent);
            }

            bool hasUra = data.UraDoraIndicators != null && data.UraDoraIndicators.Count > 0;
            if (uraDoraSection != null) uraDoraSection.SetActive(hasUra);

            if (hasUra && uraDoraIndicatorParent != null)
            {
                foreach (var ind in data.UraDoraIndicators)
                    SpawnTile(ind, uraDoraIndicatorParent);
            }
        }

        private void DisplayYakuList(AgariResultData data)
        {
            if (yakuListParent == null || yakuEntryPrefab == null) return;

            foreach (var entry in data.Yakus)
            {
                var go = Instantiate(yakuEntryPrefab, yakuListParent);
                go.SetActive(true);
                _spawnedItems.Add(go);

                var texts = go.GetComponentsInChildren<TMP_Text>(true);
                if (texts.Length >= 2)
                {
                    texts[0].text = entry.Name;
                    texts[1].text = entry.IsYakuman ? "역만" : $"{entry.Han}판";
                }
                else if (texts.Length == 1)
                {
                    texts[0].text = entry.IsYakuman
                        ? $"{entry.Name} (역만)"
                        : $"{entry.Name}  {entry.Han}판";
                }
            }
        }

        private void DisplayScoreInfo(AgariResultData data)
        {
            if (scoreClassText != null)
            {
                string classLabel = ClassLabel(data.ScoreClass, data.YakumanCount);
                scoreClassText.text = classLabel;
                scoreClassText.gameObject.SetActive(!string.IsNullOrEmpty(classLabel));
            }

            if (fuHanText != null)
            {
                if (data.YakumanCount > 0)
                    fuHanText.text = "";
                else
                    fuHanText.text = $"{data.Han}판  {data.Fu}부";
            }

            if (totalGainText != null)
                totalGainText.text = $"{data.TotalGain:N0} 점";

            if (paymentText != null)
                paymentText.text = data.PaymentDescription ?? "";
        }

        private static string ClassLabel(ScoreClass cls, int yakumanCount)
        {
            switch (cls)
            {
                case ScoreClass.Mangan: return "만 관 (満貫)";
                case ScoreClass.Haneman: return "하 네 만 (跳満)";
                case ScoreClass.Baiman: return "배 만 (倍満)";
                case ScoreClass.Sanbaiman: return "삼 배 만 (三倍満)";
                case ScoreClass.Yakuman:
                    if (yakumanCount >= 2) return $"{yakumanCount}배  역  만 (役満)";
                    return "역  만 (役満)";
                default: return "";
            }
        }

        private void SpawnTile(TileData tile, RectTransform parent)
        {
            if (tilePrefab == null || tile == null || parent == null) return;

            var go = Instantiate(tilePrefab, parent);
            go.SetActive(true);
            _spawnedItems.Add(go);

            var dragUI = go.GetComponent<TileDragUI>();
            if (dragUI != null) dragUI.enabled = false;
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) cg.blocksRaycasts = false;

            var view = go.GetComponent<TileView>();
            if (view != null)
            {
                var sprite = tileDatabase != null ? tileDatabase.GetSprite(tile) : null;
                view.SetTile(tile, sprite);
            }
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawnedItems)
                if (go != null) Destroy(go);
            _spawnedItems.Clear();
        }

        private void HandleNextClicked()
        {
            Hide();
            OnNextButtonClicked?.Invoke();
        }
    }
}
