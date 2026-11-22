using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 플레이어별 버림패 표시 UI v3.
    /// 
    /// 특정 플레이어의 버림패만 표시. 각 플레이어 앞에 배치.
    /// 7장마다 자동 줄바꿈 (마작 스타일).
    /// 
    /// Inspector 구조 예:
    ///   PlayerDiscardArea_P0 (사용자 앞)
    ///   ├── Row1 (Horizontal Layout Group) - 자동 생성
    ///   ├── Row2 - 자동 생성
    ///   └── ...
    /// 
    /// CardGameManager에 각 플레이어별로 하나씩 배치.
    /// </summary>
    public class PlayerDiscardPileView : MonoBehaviour
    {
        [Header("Player Assignment")]
        [Tooltip("표시할 플레이어 인덱스 (0~3)")]
        [SerializeField, Range(0, 3)] private int playerIndex = 0;

        [Header("References")]
        [Tooltip("행들이 세로로 쌓일 부모. Vertical Layout Group 필요.")]
        [SerializeField] private RectTransform rowsContainer;

        [Tooltip("카드 프리팹 (기존 CardPrefab 재활용)")]
        [SerializeField] private GameObject cardPrefab;

        [Header("Layout Settings")]
        [Tooltip("한 행당 카드 수 (기본 7장)")]
        [SerializeField, Range(3, 12)] private int cardsPerRow = 7;

        [Tooltip("카드 크기")]
        [SerializeField, Range(0.3f, 1f)] private float cardScale = 0.5f;

        [Tooltip("카드 사이 간격")]
        [SerializeField] private float cardSpacing = 3f;

        [Tooltip("행 사이 간격")]
        [SerializeField] private float rowSpacing = 5f;

        [Tooltip("최대 표시 행 수 (0이면 무제한)")]
        [SerializeField] private int maxRows = 0;

        [Tooltip("행 정렬 방향")]
        [SerializeField] private TextAnchor rowAlignment = TextAnchor.MiddleCenter;

        [Header("Optional")]
        [SerializeField] private TMP_Text countText;

        private readonly List<CardData> _discardedCards = new List<CardData>();
        private readonly List<GameObject> _spawnedRows = new List<GameObject>();
        private readonly List<GameObject> _spawnedCards = new List<GameObject>();

        private void OnEnable()
        {
            CardGameEvents.OnCardDiscarded += HandleCardDiscarded;
            CardGameEvents.OnDealStarted += HandleDealStarted;
        }

        private void OnDisable()
        {
            CardGameEvents.OnCardDiscarded -= HandleCardDiscarded;
            CardGameEvents.OnDealStarted -= HandleDealStarted;
        }

        private void Awake()
        {
            EnsureRowsContainerHasLayoutGroup();
        }

        private void EnsureRowsContainerHasLayoutGroup()
        {
            if (rowsContainer == null) return;
            var vlg = rowsContainer.GetComponent<VerticalLayoutGroup>();
            if (vlg == null)
            {
                vlg = rowsContainer.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            vlg.spacing = rowSpacing;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = false;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
        }

        private void HandleDealStarted()
        {
            _discardedCards.Clear();
            RefreshDisplay();
        }

        private void HandleCardDiscarded(int fromPlayerIndex, CardData card)
        {
            // 이 컴포넌트가 담당하는 플레이어의 버림만 처리
            if (fromPlayerIndex != playerIndex) return;
            if (card == null) return;

            _discardedCards.Add(card);
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            ClearSpawned();
            if (rowsContainer == null || cardPrefab == null) return;

            if (countText != null) countText.text = _discardedCards.Count.ToString();

            if (_discardedCards.Count == 0) return;

            // 7장씩 행으로 나누기
            int totalRows = Mathf.CeilToInt((float)_discardedCards.Count / cardsPerRow);

            // 최대 행 제한 (오래된 행 숨김)
            int startRow = 0;
            if (maxRows > 0 && totalRows > maxRows)
            {
                startRow = totalRows - maxRows;
            }

            for (int r = startRow; r < totalRows; r++)
            {
                var rowGO = CreateRow();
                _spawnedRows.Add(rowGO);

                int cardStart = r * cardsPerRow;
                int cardEnd = Mathf.Min(cardStart + cardsPerRow, _discardedCards.Count);

                for (int i = cardStart; i < cardEnd; i++)
                {
                    SpawnCard(_discardedCards[i], rowGO.transform);
                }
            }
        }

        private GameObject CreateRow()
        {
            var rowGO = new GameObject($"Row_{_spawnedRows.Count}", typeof(RectTransform));
            rowGO.transform.SetParent(rowsContainer, false);

            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = cardSpacing;
            hlg.childAlignment = rowAlignment;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // ContentSizeFitter로 행 높이/너비 자동 조정
            var fitter = rowGO.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return rowGO;
        }

        private void SpawnCard(CardData card, Transform parent)
        {
            var go = Instantiate(cardPrefab, parent);
            go.SetActive(true);
            _spawnedCards.Add(go);

            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.localScale = Vector3.one * cardScale;

            var view = go.GetComponent<CardView>();
            if (view != null) view.SetCard(card);

            // 클릭 비활성화
            var click = go.GetComponent<CardClickHandler>();
            if (click != null) click.enabled = false;
            var imgs = go.GetComponentsInChildren<Image>();
            foreach (var img in imgs) img.raycastTarget = false;

            // LayoutElement로 크기 힌트 (없으면 Layout Group이 사이즈 계산 못 함)
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (rt != null)
            {
                le.preferredWidth = rt.rect.width * cardScale;
                le.preferredHeight = rt.rect.height * cardScale;
            }
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawnedCards)
                if (go != null) Destroy(go);
            _spawnedCards.Clear();

            foreach (var row in _spawnedRows)
                if (row != null) Destroy(row);
            _spawnedRows.Clear();
        }
    }
}
