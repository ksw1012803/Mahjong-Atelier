using UnityEngine;
using CardGame.Core;

namespace CardGame.UI3D
{
    /// <summary>
    /// 3D 카드 뷰. GameObject에 붙여서 CardData와 3D 프리팹 연결.
    /// 
    /// 사용법:
    ///   1) Card3DManager가 CardData로부터 프리팹 로드
    ///   2) Instantiate 후 Card3DView 컴포넌트 추가
    ///   3) SetCard(card) 호출로 데이터 세팅
    /// </summary>
    public class Card3DView : MonoBehaviour
    {
        public CardData CardData { get; private set; }

        [Header("Runtime References")]
        [SerializeField] private GameObject cardModelInstance;

        [Header("Fallback (Wild/미제작 카드)")]
        [Tooltip("프리팹 없을 때 사용할 대체 오브젝트 (예: 기본 큐브)")]
        [SerializeField] private GameObject fallbackPrefab;

        [Header("Display Options")]
        [Tooltip("카드 크기 (스케일 배율)")]
        [SerializeField] private float cardScale = 1f;

        [Tooltip("뒷면 표시 여부 (다른 플레이어 손패용)")]
        [SerializeField] private bool showBack = false;

        public void SetCard(CardData card, bool asBack = false)
        {
            CardData = card;
            showBack = asBack;
            RefreshModel();
        }

        private void RefreshModel()
        {
            // 기존 모델 제거
            if (cardModelInstance != null)
            {
                DestroyImmediate(cardModelInstance);
            }

            if (CardData == null) return;

            GameObject prefab = null;

            if (!showBack && !CardData.Kind.IsSpecial)
            {
                // 앞면 & 일반 카드 → 색_테마_번호 프리팹
                prefab = Card3DResourceMapper.LoadPrefab(CardData);
            }

            // 프리팹 없거나 Wild/특수/뒷면 → fallback
            if (prefab == null && fallbackPrefab != null)
            {
                prefab = fallbackPrefab;
            }

            if (prefab == null)
            {
                Debug.LogWarning($"[Card3DView] 표시할 모델 없음: {CardData}");
                return;
            }

            cardModelInstance = Instantiate(prefab, transform);
            cardModelInstance.transform.localPosition = Vector3.zero;
            cardModelInstance.transform.localRotation = Quaternion.identity;
            cardModelInstance.transform.localScale = Vector3.one * cardScale;
        }

        /// <summary>뒤집기 (앞↔뒤 전환).</summary>
        public void FlipTo(bool showBackNow)
        {
            if (showBack == showBackNow) return;
            showBack = showBackNow;
            RefreshModel();
        }
    }
}
