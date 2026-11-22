using UnityEngine;
using UnityEngine.EventSystems;

namespace MahjongAtelier.UI
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasGroup))]
    public class TileDragUI : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("References")]
        public RectTransform handArea;
        public Canvas canvas;
        public HandUIManager handManager;

        [Header("Drag Option")]
        public bool lockY = true;
        public float dragLiftY = 20f;

        [Header("Smooth Move")]
        public float smoothTime = 0.06f;
        public float maxSpeed = 5000f;

        [Header("Hover Scale")]
        public float hoverScale = 1.12f;
        public float scaleLerpSpeed = 16f;

        private RectTransform rectTransform;
        private CanvasGroup canvasGroup;

        private Vector2 originalAnchoredPos;
        private Vector2 targetAnchoredPos;
        private Vector2 smoothVelocity;

        private int slotIndex;
        private bool isDragging = false;
        private bool isHovered = false;
        private Vector3 targetScale = Vector3.one;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            targetAnchoredPos = rectTransform.anchoredPosition;
            targetScale = Vector3.one;
            rectTransform.localScale = Vector3.one;
        }

        private void Update()
        {
            if (!isDragging)
            {
                rectTransform.anchoredPosition = Vector2.SmoothDamp(
                    rectTransform.anchoredPosition,
                    targetAnchoredPos,
                    ref smoothVelocity,
                    smoothTime,
                    maxSpeed,
                    Time.unscaledDeltaTime);
            }

            rectTransform.localScale = Vector3.Lerp(
                rectTransform.localScale,
                targetScale,
                Time.unscaledDeltaTime * scaleLerpSpeed);
        }

        public void SetSlotIndex(int index) => slotIndex = index;
        public int GetSlotIndex() => slotIndex;

        public void MoveToSlot(Vector2 slotPos) => targetAnchoredPos = slotPos;

        public void ForceSetPosition(Vector2 pos)
        {
            targetAnchoredPos = pos;
            rectTransform.anchoredPosition = pos;
            smoothVelocity = Vector2.zero;
        }

        public Vector2 GetCurrentPos() => rectTransform.anchoredPosition;

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            UpdateTargetScale();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            UpdateTargetScale();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            isDragging = true;
            originalAnchoredPos = rectTransform.anchoredPosition;
            smoothVelocity = Vector2.zero;

            canvasGroup.blocksRaycasts = false;
            rectTransform.SetAsLastSibling();

            UpdateTargetScale();
            handManager.BeginPreview(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 nextPos = rectTransform.anchoredPosition + eventData.delta / canvas.scaleFactor;
            if (lockY)
                nextPos.y = originalAnchoredPos.y + dragLiftY;

            rectTransform.anchoredPosition = nextPos;
            ClampToHandArea();
            handManager.PreviewInsert(this);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            isDragging = false;
            canvasGroup.blocksRaycasts = true;

            UpdateTargetScale();
            handManager.CommitInsert(this);
        }

        private void UpdateTargetScale()
        {
            targetScale = isHovered ? Vector3.one * hoverScale : Vector3.one;
        }

        private void ClampToHandArea()
        {
            Vector3[] areaCorners = new Vector3[4];
            handArea.GetWorldCorners(areaCorners);

            Vector3 pos = rectTransform.position;
            float halfW = rectTransform.rect.width * rectTransform.lossyScale.x * 0.5f;
            float halfH = rectTransform.rect.height * rectTransform.lossyScale.y * 0.5f;

            float left = areaCorners[0].x + halfW;
            float right = areaCorners[3].x - halfW;
            float bottom = areaCorners[0].y + halfH;
            float top = areaCorners[1].y - halfH;

            pos.x = Mathf.Clamp(pos.x, left, right);
            if (!lockY) pos.y = Mathf.Clamp(pos.y, bottom, top);

            rectTransform.position = pos;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (handManager == null) return;
            handManager.OnTileClicked(this);
        }
    }
}
