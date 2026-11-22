using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 버림패 열람 패널의 한 칸: 카드 + 누가 버렸는지 + 순번.
    ///
    /// 프리팹 구성 (예):
    ///   Entry (이 컴포넌트)
    ///   ├─ Frame (Image, 테두리 — 플레이어 색)         → frameImage
    ///   ├─ Card (기존 카드 프리팹 복제, CardView 포함)   → cardView
    ///   ├─ OwnerText (TMP, "P1" / 닉네임)              → ownerText
    ///   └─ OrderText (TMP, "#12")                      → orderText (선택)
    /// </summary>
    public class DiscardHistoryEntry : MonoBehaviour
    {
        [SerializeField] private CardView cardView;
        [SerializeField] private Image frameImage;
        [SerializeField] private TMP_Text ownerText;
        [SerializeField] private TMP_Text orderText;

        public void Bind(CardData card, string ownerName, Color ownerColor, int order)
        {
            if (cardView == null) cardView = GetComponentInChildren<CardView>(true);
            if (cardView != null) cardView.SetCard(card);

            // 손패용 상호작용 컴포넌트가 딸려왔으면 제거 (드래그/호버 방지)
            StripInteraction(cardView != null ? cardView.gameObject : gameObject);

            if (ownerText != null) ownerText.text = ownerName;
            if (frameImage != null) frameImage.color = ownerColor;
            if (orderText != null) orderText.text = $"#{order}";
        }

        private static void StripInteraction(GameObject go)
        {
            if (go == null) return;
            var click = go.GetComponent<CardClickHandler>();
            if (click != null) Destroy(click);
            var hover = go.GetComponent<CardHoverHandler>();
            if (hover != null) Destroy(hover);
            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 1f; cg.blocksRaycasts = false; cg.interactable = false; }
        }
    }
}
