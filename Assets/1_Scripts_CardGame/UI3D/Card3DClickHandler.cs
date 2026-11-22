using System;
using UnityEngine;
using CardGame.Core;

namespace CardGame.UI3D
{
    /// <summary>
    /// 3D 카드 클릭 처리.
    /// 
    /// 3D는 UnityUI의 IPointerClickHandler 대신 
    /// Physics 레이캐스트를 사용. 각 카드에 Collider 필요.
    /// 
    /// Card3DHandManager가 매 프레임 마우스 위치에서 레이캐스트해서
    /// 히트된 카드의 이 컴포넌트를 호출.
    /// </summary>
    public class Card3DClickHandler : MonoBehaviour
    {
        private CardData _card;
        private Action<CardData> _onClicked;

        [Header("Hover Effect")]
        [Tooltip("호버 시 위로 이동할 거리 (Y축)")]
        [SerializeField] private float hoverLift = 0.1f;

        [Tooltip("호버 애니메이션 속도")]
        [SerializeField] private float hoverSpeed = 10f;

        private Vector3 _originalLocalPosition;
        private bool _isHovering = false;
        private bool _positionCaptured = false;

        [Header("Debug")]
        [SerializeField] private bool debugLog = false;

        public void Setup(CardData card, Action<CardData> onClicked)
        {
            _card = card;
            _onClicked = onClicked;

            if (!_positionCaptured)
            {
                _originalLocalPosition = transform.localPosition;
                _positionCaptured = true;
            }

            if (debugLog) Debug.Log($"[Card3DClick Setup] {card}", this);
        }

        public void UpdateOriginalPosition()
        {
            _originalLocalPosition = transform.localPosition;
            _positionCaptured = true;
        }

        public void SetHovering(bool hovering)
        {
            _isHovering = hovering;
        }

        private void Update()
        {
            if (!_positionCaptured) return;

            var target = _isHovering
                ? _originalLocalPosition + Vector3.up * hoverLift
                : _originalLocalPosition;

            transform.localPosition = Vector3.Lerp(
                transform.localPosition,
                target,
                Time.deltaTime * hoverSpeed);
        }

        public void TriggerClick()
        {
            if (_card == null)
            {
                if (debugLog) Debug.LogWarning("[Card3DClick] _card null");
                return;
            }
            if (debugLog) Debug.Log($"[Card3DClick 클릭!] {_card}", this);
            _onClicked?.Invoke(_card);
        }
    }
}
