using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CardGame.Core;

namespace CardGame.UI
{
    /// <summary>
    /// 선택한 캐릭터 이미지를 Image에 자동 반영 (로비 전신 캐릭터, 로비 프로필 초상화 등).
    /// 캐릭터가 바뀌면 살짝 페이드하며 교체.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class CharacterSpriteBinder : MonoBehaviour
    {
        public enum Kind { Portrait, FullBody }

        [SerializeField] private Kind kind = Kind.Portrait;
        [Tooltip("교체 시 RectTransform 크기를 이미지 원본 비율에 맞춤 (전신 이미지 크기가 서로 다를 때).")]
        [SerializeField] private bool matchSpriteSize = true;
        [SerializeField] private float swapFadeDuration = 0.15f;
        [Tooltip("0보다 크면 이미지의 투명한 부분은 클릭되지 않음 (텍스처 Read/Write 필요). 로비 캐릭터 클릭용.")]
        [Range(0f, 1f)]
        [SerializeField] private float alphaHitThreshold = 0f;

        private Image _image;

        private void Awake() => _image = GetComponent<Image>();

        private void OnEnable()
        {
            PlayerProfile.OnCharacterChanged += HandleChanged;
            Apply(PlayerProfile.CharacterIndex, false);
        }

        private void OnDisable()
        {
            PlayerProfile.OnCharacterChanged -= HandleChanged;
            if (_image != null) _image.DOKill();
        }

        private void HandleChanged(int index) => Apply(index, true);

        private void Apply(int index, bool animate)
        {
            var db = CharacterDatabase.Instance;
            var def = db != null ? db.Get(index) : null;
            if (def == null || _image == null) return;

            var sprite = kind == Kind.Portrait ? def.portrait : def.fullBody;
            if (sprite == null) return;

            if (!animate || swapFadeDuration <= 0f)
            {
                SetSprite(sprite);
                return;
            }

            _image.DOKill();
            var c = _image.color;
            _image.DOFade(0f, swapFadeDuration).SetLink(gameObject).OnComplete(() =>
            {
                SetSprite(sprite);
                _image.DOFade(c.a <= 0f ? 1f : c.a, swapFadeDuration).SetLink(gameObject);
            });
        }

        private void SetSprite(Sprite sprite)
        {
            _image.sprite = sprite;
            // 투명 영역 클릭 무시 — 텍스처를 읽을 수 없으면 사각형 전체로 클릭 (에러 방지)
            // (이 옵션을 쓰지 않는 Image에는 아예 손대지 않음 — 읽을 수 없는 텍스처면 0을 넣어도 예외 발생)
            if (alphaHitThreshold > 0f)
            {
                bool readable = sprite.texture != null && sprite.texture.isReadable;
                try { _image.alphaHitTestMinimumThreshold = readable ? alphaHitThreshold : 0f; }
                catch (System.InvalidOperationException)
                {
                    Debug.LogWarning($"[CharacterSpriteBinder] '{sprite.name}' 텍스처의 Read/Write를 켜야 투명 영역 클릭 무시가 동작합니다.", this);
                }
            }
            if (matchSpriteSize)
            {
                var rt = (RectTransform)transform;
                rt.sizeDelta = sprite.rect.size;
            }
        }
    }
}
