using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 카드 시각 표시 v4 — Database 기반 스프라이트 조회.
    /// </summary>
    public class CardView : MonoBehaviour
    {
        public CardData CardData { get; private set; }

        [Header("References")]
        [SerializeField] private Image spriteImage;
        [SerializeField] private Sprite fallbackSprite;
        [SerializeField] private TMP_Text specialText;

        [Header("Fallback (Database 없을 때)")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text numberText;

        private static CardDatabase _database;

        /// <summary>CardGameManager가 초기화 시 호출.</summary>
        public static void SetDatabase(CardDatabase db) => _database = db;

        public void SetCard(CardData card)
        {
            CardData = card;
            if (card == null) { ClearDisplay(); return; }

            Sprite sprite = null;
            if (_database != null && card.Definition != null)
                sprite = _database.GetSprite(card.Definition);

            if (sprite != null) ShowSprite(sprite, card);
            else ShowFallback(card);
        }

        private void ShowSprite(Sprite sprite, CardData card)
        {
            if (spriteImage != null)
            {
                spriteImage.gameObject.SetActive(true);
                spriteImage.sprite = sprite;
                spriteImage.color = Color.white;
            }
            if (backgroundImage != null && backgroundImage != spriteImage)
                backgroundImage.gameObject.SetActive(false);
            if (numberText != null) numberText.gameObject.SetActive(false);
            if (specialText != null)
                specialText.gameObject.SetActive(card.Kind.IsSpecial && sprite == fallbackSprite);
        }

        private void ShowFallback(CardData card)
        {
            if (fallbackSprite != null && spriteImage != null)
            {
                spriteImage.gameObject.SetActive(true);
                spriteImage.sprite = fallbackSprite;
            }
            else if (spriteImage != null) spriteImage.gameObject.SetActive(false);

            if (backgroundImage != null)
            {
                backgroundImage.gameObject.SetActive(true);
                backgroundImage.color = ColorFor(card.Kind);
            }
            if (card.Kind.IsSpecial)
            {
                if (numberText != null) numberText.gameObject.SetActive(false);
                if (specialText != null)
                {
                    specialText.gameObject.SetActive(true);
                    specialText.text = SpecialName(card.Kind.Special);
                }
            }
            else
            {
                if (numberText != null)
                {
                    numberText.gameObject.SetActive(true);
                    numberText.text = card.Kind.Number.ToString();
                }
                if (specialText != null) specialText.gameObject.SetActive(false);
            }
        }

        private void ClearDisplay()
        {
            if (spriteImage != null) spriteImage.sprite = null;
            if (numberText != null) numberText.text = "";
            if (specialText != null) specialText.text = "";
        }

        private Color ColorFor(CardKind kind)
        {
            if (kind.IsSpecial) return new Color(0.5f, 0.3f, 0.7f);
            return kind.Color switch
            {
                CardColor.Red => new Color(0.9f, 0.3f, 0.3f),
                CardColor.Blue => new Color(0.3f, 0.5f, 0.9f),
                CardColor.Green => new Color(0.3f, 0.8f, 0.4f),
                CardColor.Yellow => new Color(0.95f, 0.85f, 0.3f),
                _ => Color.gray
            };
        }

        private string SpecialName(SpecialCardType t) => t switch
        {
            SpecialCardType.Wild => "W",
            SpecialCardType.Swap => "S",
            SpecialCardType.Peek => "P",
            SpecialCardType.Chaos => "C",
            _ => "?"
        };
    }
}