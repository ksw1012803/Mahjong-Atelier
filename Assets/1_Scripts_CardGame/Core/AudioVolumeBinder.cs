using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// AudioSource 볼륨을 설정의 배경음/효과음 볼륨에 맞춤.
    /// BGM을 재생하는 AudioSource에는 Bgm, 효과음 AudioSource에는 Sfx로 붙이면 됨.
    /// 인스펙터에서 정한 원래 볼륨 × 설정 볼륨으로 적용.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioVolumeBinder : MonoBehaviour
    {
        [SerializeField] private AudioCategory category = AudioCategory.Bgm;

        private AudioSource _source;
        private float _baseVolume = 1f;
        private bool _captured;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _baseVolume = _source.volume;
            _captured = true;
        }

        private void OnEnable()
        {
            GameAudioSettings.OnChanged += Apply;
            Apply();
        }

        private void OnDisable() => GameAudioSettings.OnChanged -= Apply;

        private void Apply()
        {
            if (_source == null || !_captured) return;
            _source.volume = _baseVolume * GameAudioSettings.Get(category);
        }
    }
}
