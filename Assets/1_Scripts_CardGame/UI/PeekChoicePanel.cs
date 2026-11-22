using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// Peek 카드 발동 시 덱 위 3장 표시 + 1장 선택 UI.
    /// 
    /// v2 변경:
    ///   - CardClickHandler 대신 Button 사용 (CardClickHandler v5.1은 버리기 전용)
    ///   - Peek은 "선택" 기능이므로 Button.onClick이 올바른 방식
    /// </summary>
    public class PeekChoicePanel : MonoBehaviour
    {
        [Header("Dialog")]
        [SerializeField] private GameObject dialogContainer;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text instructionText;

        [Header("Card Display")]
        [Tooltip("보여줄 카드 3장이 들어갈 부모")]
        [SerializeField] private RectTransform cardContainer;
        [SerializeField] private GameObject cardPrefab;

        [Header("Cancel")]
        [SerializeField] private Button cancelButton;

        public event Action<CardData> OnCardSelected;

        private List<GameObject> _spawnedCards = new List<GameObject>();
        private List<CardData> _peekedCards;

        private void Awake()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancelClicked);
        }

        private void OnDestroy()
        {
            if (cancelButton != null) cancelButton.onClick.RemoveListener(HandleCancelClicked);
        }

        public void Show(List<CardData> topCards)
        {
            _peekedCards = topCards;
            ClearSpawned();

            if (titleText != null) titleText.text = "Peek: 카드 선택";
            if (instructionText != null) instructionText.text = "덱 위 3장 중 1장을 선택하세요";

            if (cardContainer != null && cardPrefab != null)
            {
                foreach (var card in topCards)
                    SpawnCard(card);
            }

            if (dialogContainer != null) dialogContainer.SetActive(true);
        }

        public void Hide()
        {
            if (dialogContainer != null) dialogContainer.SetActive(false);
            ClearSpawned();
        }

        private void SpawnCard(CardData card)
        {
            var go = Instantiate(cardPrefab, cardContainer);
            go.SetActive(true);
            _spawnedCards.Add(go);

            var view = go.GetComponent<CardView>();
            if (view != null) view.SetCard(card);

            // ★ CardClickHandler 비활성화 (버리기 기능 방지)
            //   CardClickHandler v5.1은 gameManager.RequestDiscard를 호출하므로
            //   Peek 선택 UI에서는 사용하면 안 됨.
            var ch = go.GetComponent<CardClickHandler>();
            if (ch != null) ch.enabled = false;

            // ★ Button으로 선택 이벤트 바인딩
            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();

            // 클로저로 카드 캡처
            CardData captured = card;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => HandleCardClicked(captured));
        }

        private void HandleCardClicked(CardData card)
        {
            Debug.Log($"[PeekChoicePanel] 선택: {card}");
            Hide();
            OnCardSelected?.Invoke(card);
        }

        private void HandleCancelClicked()
        {
            Debug.Log("[PeekChoicePanel] 취소 (첫 카드 선택)");
            if (_peekedCards != null && _peekedCards.Count > 0)
            {
                var first = _peekedCards[0];
                Hide();
                OnCardSelected?.Invoke(first);
            }
            else Hide();
        }

        private void ClearSpawned()
        {
            foreach (var go in _spawnedCards)
                if (go != null) Destroy(go);
            _spawnedCards.Clear();
        }
    }
}