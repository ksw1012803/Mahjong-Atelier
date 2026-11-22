using UnityEngine;
using UnityEngine.EventSystems;

namespace CardGame.UI
{
    /// <summary>
    /// 손패 카드 호버 핸들러 — 포인터가 카드 위에 올라가면
    /// CardHandUIManager에 알려 "떠오름 + 주변 밀림" 연출을 트리거.
    /// 드래그 중에는 호버 이벤트 무시.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardHoverHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CardHandUIManager _handManager;

        public void Setup(CardHandUIManager manager)
        {
            _handManager = manager;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_handManager == null) return;
            if (CardHandUIManager.IsAnyDragging) return;
            _handManager.OnHoverStart(gameObject);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_handManager == null) return;
            _handManager.OnHoverEnd(gameObject);
        }
    }
}
