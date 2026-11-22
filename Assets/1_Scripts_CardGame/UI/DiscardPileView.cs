using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 공용 버림패 표시 v2 — 플레이어별 색상 구분 옵션.
    /// </summary>
    public class DiscardPileView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform discardContainer;
        [SerializeField] private GameObject cardPrefab;

        [Header("Optional Info")]
        [SerializeField] private TMP_Text countText;

        [Header("Display Settings")]
        [Tooltip("최대 표시 카드 수. 0이면 전체 표시")]
        [SerializeField] private int maxDisplayCards = 15;

        [Tooltip("최신 카드가 오른쪽에 위치 (true) / 왼쪽 (false)")]
        [SerializeField] private bool newestOnRight = true;

        [SerializeField, Range(0.3f, 1f)] private float cardScale = 0.6f;

        [Header("Player Color Coding (선택)")]
        [SerializeField] private bool showPlayerColors = false;

        [SerializeField] private Color[] playerColors = new Color[4]
        {
            new Color(1f, 0.6f, 0.6f),
            new Color(0.6f, 0.8f, 1f),
            new Color(0.8f, 1f, 0.6f),
            new Color(1f, 0.9f, 0.5f)
        };

        private sealed class DiscardEntry
        {
            public CardData Card;
            public int PlayerIndex;
        }

        private readonly List<DiscardEntry> _discarded = new List<DiscardEntry>();
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

        private void HandleDealStarted()
        {
            _discarded.Clear();
            RefreshDisplay();
        }

        private void HandleCardDiscarded(int playerIndex, CardData card)
        {
            if (card == null) return;
            _discarded.Add(new DiscardEntry { Card = card, PlayerIndex = playerIndex });
            RefreshDisplay();
        }

        private void RefreshDisplay()
        {
            ClearSpawned();
            if (discardContainer == null || cardPrefab == null) return;

            if (countText != null) countText.text = _discarded.Count.ToString();

            List<DiscardEntry> toDisplay;
            if (maxDisplayCards <= 0 || _discarded.Count <= maxDisplayCards)
                toDisplay = new List<DiscardEntry>(_discarded);
            else
                toDisplay = _discarded.GetRange(
                    _discarded.Count - maxDisplayCards, maxDisplayCards);

            if (!newestOnRight) toDisplay.Reverse();

            foreach (var entry in toDisplay)
                SpawnCard(entry);
        }

        private void SpawnCard(DiscardEntry entry)
        {
            var go = Instantiate(cardPrefab, discardContainer);
            go.SetActive(true);
            _spawnedCards.Add(go);

            var rt = go.GetComponent<RectTransform>();
            if (rt != null) rt.localScale = Vector3.one * cardScale;

            var view = go.GetComponent<CardView>();
            if (view != null) view.SetCard(entry.Card);

            var click = go.GetComponent<CardClickHandler>();
            if (click != null) click.enabled = false;
            var imgs = go.GetComponentsInChildren<Image>();
            foreach (var img in imgs) img.raycastTarget = false;

            if (showPlayerColors && entry.PlayerIndex >= 0 && entry.PlayerIndex < playerColors.Length)
            {
                AddPlayerStripe(go, playerColors[entry.PlayerIndex]);
            }
        }

        private void AddPlayerStripe(GameObject cardGO, Color stripeColor)
        {
            var stripe = new GameObject("PlayerStripe", typeof(RectTransform), typeof(Image));
            stripe.transform.SetParent(cardGO.transform, false);

            var srt = stripe.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0, 0);
            srt.anchorMax = new Vector2(1, 0);
            srt.sizeDelta = new Vector2(0, 8);
            srt.anchoredPosition = Vector2.zero;

            var simg = stripe.GetComponent<Image>();
            simg.color = stripeColor;
            simg.raycastTarget = false;
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawnedCards)
                if (go != null) Destroy(go);
            _spawnedCards.Clear();
        }
    }
}
