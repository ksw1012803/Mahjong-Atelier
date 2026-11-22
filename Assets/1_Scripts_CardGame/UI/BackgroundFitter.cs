using UnityEngine;
using UnityEngine.UI;

namespace CardGame.UI
{
    /// <summary>
    /// 배경 이미지를 비율 유지한 채 부모(Canvas/화면)를 "꽉 채움" (CSS background-size: cover).
    ///
    /// Unity Image의 Preserve Aspect는 "contain"(여백 생김)이고,
    /// Anchor stretch는 비율 왜곡이 생김. 이 컴포넌트는 둘 다 해결:
    ///   - 짧은 변을 부모에 맞추고 긴 변은 잘림 → 어떤 비율(16:9·21:9·4:3)에서도 여백·왜곡 없음
    ///   - Align으로 어느 부분을 남길지 지정 (0,0=좌하 / 0.5,0.5=중앙 / 1,1=우상)
    ///
    /// 사용: 배경 Image(또는 RawImage)에 추가. Anchor는 중앙(0.5,0.5) 권장 — 스크립트가 크기를 직접 잡음.
    /// 해상도가 바뀌면 자동 재계산.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public class BackgroundFitter : MonoBehaviour
    {
        public enum FitMode
        {
            /// <summary>비율 유지 + 꽉 채움 (가장자리 잘림). 배경 추천.</summary>
            Cover,
            /// <summary>비율 유지 + 전부 보임 (여백 생김). 로고·일러스트.</summary>
            Contain,
        }

        [SerializeField] private FitMode mode = FitMode.Cover;

        [Tooltip("Cover 모드에서 잘릴 때 어느 부분을 남길지. (0.5, 0.5) = 중앙 유지.")]
        [SerializeField] private Vector2 align = new Vector2(0.5f, 0.5f);

        [Tooltip("원본 비율을 직접 지정 (0이면 Image/RawImage의 스프라이트·텍스처에서 자동).")]
        [SerializeField] private float overrideAspect = 0f;

        [Tooltip("살짝 여유를 둬서 가장자리 1px 틈 방지 (1.0 = 정확히, 1.01 = 1% 크게).")]
        [SerializeField, Range(1f, 1.1f)] private float overscan = 1.005f;

        private RectTransform _rt;
        private RectTransform _parent;
        private Image _image;
        private RawImage _raw;
        private Vector2 _lastParentSize;
        private float _lastAspect;

        private void OnEnable()
        {
            Cache();
            Fit(true);
        }

        private void OnRectTransformDimensionsChange()
        {
            Fit(false);
        }

        private void Update()
        {
            // 부모 크기 변화(해상도 변경)는 자식에 콜백이 안 올 수 있어 매 프레임 가볍게 체크
            if (_parent == null) Cache();
            if (_parent == null) return;
            var ps = _parent.rect.size;
            float a = GetAspect();
            if (ps != _lastParentSize || !Mathf.Approximately(a, _lastAspect)) Fit(true);
        }

        private void Cache()
        {
            _rt = GetComponent<RectTransform>();
            _parent = _rt.parent as RectTransform;
            _image = GetComponent<Image>();
            _raw = GetComponent<RawImage>();
        }

        private float GetAspect()
        {
            if (overrideAspect > 0f) return overrideAspect;
            if (_image != null && _image.sprite != null)
            {
                var r = _image.sprite.rect;
                if (r.height > 0f) return r.width / r.height;
            }
            if (_raw != null && _raw.texture != null && _raw.texture.height > 0)
                return (float)_raw.texture.width / _raw.texture.height;
            return 16f / 9f;
        }

        [ContextMenu("Fit Now")]
        public void FitNow() => Fit(true);

        private void Fit(bool force)
        {
            if (_rt == null || _parent == null) Cache();
            if (_rt == null || _parent == null) return;

            Vector2 ps = _parent.rect.size;
            if (ps.x <= 0f || ps.y <= 0f) return;

            float aspect = GetAspect();
            if (!force && ps == _lastParentSize && Mathf.Approximately(aspect, _lastAspect)) return;
            _lastParentSize = ps;
            _lastAspect = aspect;

            float parentAspect = ps.x / ps.y;
            Vector2 size;

            bool coverWide = parentAspect > aspect;  // 부모가 더 넓음 → 가로를 맞춤
            if (mode == FitMode.Cover ? coverWide : !coverWide)
                size = new Vector2(ps.x, ps.x / aspect);
            else
                size = new Vector2(ps.y * aspect, ps.y);

            if (mode == FitMode.Cover) size *= overscan;

            // 앵커를 중앙 고정, 크기 직접 지정
            _rt.anchorMin = _rt.anchorMax = new Vector2(0.5f, 0.5f);
            _rt.pivot = new Vector2(0.5f, 0.5f);
            _rt.sizeDelta = size;

            // 잘리는 방향으로 align 만큼 오프셋 (0.5 = 중앙)
            Vector2 overflow = size - ps;
            Vector2 offset = new Vector2(
                (0.5f - align.x) * Mathf.Max(0f, overflow.x),
                (0.5f - align.y) * Mathf.Max(0f, overflow.y));
            _rt.anchoredPosition = offset;
        }
    }
}
