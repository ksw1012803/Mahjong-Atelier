using System;
using UnityEngine;

namespace CardGame.Core
{
    public enum AudioCategory { Bgm, Sfx }

    /// <summary>
    /// 소리 설정 — 전체 / 배경음 / 효과음 볼륨 + 음소거. PlayerPrefs에 저장.
    ///
    ///   - 전체 볼륨·음소거 → AudioListener.volume (게임의 모든 소리에 적용)
    ///   - 배경음·효과음 → AudioVolumeBinder를 붙인 AudioSource에 적용
    ///   - 새 효과음을 코드로 재생할 때: source.PlayOneShot(clip, GameAudioSettings.Get(AudioCategory.Sfx))
    ///   - 게임 시작 시 자동 적용 (어느 씬에서 시작해도)
    /// </summary>
    public static class GameAudioSettings
    {
        private const string K_MASTER = "audio.master";
        private const string K_BGM = "audio.bgm";
        private const string K_SFX = "audio.sfx";
        private const string K_MUTE = "audio.mute";

        public const float DefaultMaster = 0.8f;
        public const float DefaultBgm = 0.7f;
        public const float DefaultSfx = 0.8f;

        /// <summary>값이 바뀌면 호출 (AudioVolumeBinder가 구독).</summary>
        public static event Action OnChanged;

        private static bool _loaded;
        private static float _master, _bgm, _sfx;
        private static bool _muted;

        public static float Master { get { EnsureLoaded(); return _master; } set { EnsureLoaded(); _master = Mathf.Clamp01(value); Changed(); } }
        public static float Bgm { get { EnsureLoaded(); return _bgm; } set { EnsureLoaded(); _bgm = Mathf.Clamp01(value); Changed(); } }
        public static float Sfx { get { EnsureLoaded(); return _sfx; } set { EnsureLoaded(); _sfx = Mathf.Clamp01(value); Changed(); } }
        public static bool Muted { get { EnsureLoaded(); return _muted; } set { EnsureLoaded(); _muted = value; Changed(); } }

        /// <summary>카테고리 볼륨 (전체 볼륨·음소거는 AudioListener가 따로 처리하므로 포함하지 않음).</summary>
        public static float Get(AudioCategory category) => category == AudioCategory.Bgm ? Bgm : Sfx;

        public static void ResetToDefaults()
        {
            EnsureLoaded();
            _master = DefaultMaster; _bgm = DefaultBgm; _sfx = DefaultSfx; _muted = false;
            Changed();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnBoot()
        {
            _loaded = false;
            EnsureLoaded();
            ApplyListener();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _master = PlayerPrefs.GetFloat(K_MASTER, DefaultMaster);
            _bgm = PlayerPrefs.GetFloat(K_BGM, DefaultBgm);
            _sfx = PlayerPrefs.GetFloat(K_SFX, DefaultSfx);
            _muted = PlayerPrefs.GetInt(K_MUTE, 0) == 1;
        }

        private static void Changed()
        {
            PlayerPrefs.SetFloat(K_MASTER, _master);
            PlayerPrefs.SetFloat(K_BGM, _bgm);
            PlayerPrefs.SetFloat(K_SFX, _sfx);
            PlayerPrefs.SetInt(K_MUTE, _muted ? 1 : 0);
            PlayerPrefs.Save();
            ApplyListener();
            OnChanged?.Invoke();
        }

        private static void ApplyListener() => AudioListener.volume = _muted ? 0f : _master;
    }
}
