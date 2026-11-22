using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    /// <summary>화면 모드 (UI 표시용 단순화). Unity FullScreenMode와 1:1 매핑.</summary>
    public enum DisplayModeOption
    {
        /// <summary>전체화면 (독점). 알트탭 시 최소화. 성능 최우선.</summary>
        Fullscreen = 0,
        /// <summary>테두리 없는 창 (전체화면 창). 알트탭 빠름. 기본값 권장.</summary>
        Borderless = 1,
        /// <summary>창모드.</summary>
        Windowed = 2,
    }

    /// <summary>
    /// 디스플레이·화질 설정 — 저장(PlayerPrefs) / 적용 / 조회.
    ///
    /// 사용:
    ///   DisplaySettings.ApplySaved()                — 부팅 시 (SettingsBootstrap)
    ///   var s = DisplaySettings.Load();  s.quality = 2;  DisplaySettings.Apply(s);  DisplaySettings.Save(s);
    ///   DisplaySettings.GetResolutionOptions()      — 드롭다운용 (중복 제거, 내림차순)
    /// </summary>
    public static class DisplaySettings
    {
        // ---- PlayerPrefs 키 ----
        private const string K_MODE = "disp.mode";
        private const string K_W = "disp.w";
        private const string K_H = "disp.h";
        private const string K_QUALITY = "disp.quality";
        private const string K_VSYNC = "disp.vsync";
        private const string K_FPS = "disp.fps";
        private const string K_SAVED = "disp.saved";

        [Serializable]
        public struct Data
        {
            public DisplayModeOption mode;
            public int width;
            public int height;
            public int qualityLevel;
            public bool vsync;
            /// <summary>목표 FPS. -1 = 무제한. VSync 켜져 있으면 무시됨.</summary>
            public int targetFps;

            public override string ToString() =>
                $"{mode} {width}x{height} Q{qualityLevel} vsync={vsync} fps={targetFps}";
        }

        public static readonly int[] FpsOptions = { 30, 60, 120, 144, 240, -1 };

        /// <summary>기본값: 테두리 없는 창 + 현재 모니터 해상도 + 프로젝트 기본 화질 + VSync.</summary>
        public static Data Default()
        {
            var r = Screen.currentResolution;
            return new Data
            {
                mode = DisplayModeOption.Borderless,
                width = r.width,
                height = r.height,
                qualityLevel = QualitySettings.GetQualityLevel(),
                vsync = true,
                targetFps = 60,
            };
        }

        /// <summary>현재 실제 적용 상태.</summary>
        public static Data Current()
        {
            return new Data
            {
                mode = FromUnityMode(Screen.fullScreenMode),
                width = Screen.width,
                height = Screen.height,
                qualityLevel = QualitySettings.GetQualityLevel(),
                vsync = QualitySettings.vSyncCount > 0,
                targetFps = Application.targetFrameRate,
            };
        }

        public static bool HasSaved() => PlayerPrefs.GetInt(K_SAVED, 0) == 1;

        public static Data Load()
        {
            var d = Default();
            if (!HasSaved()) return d;

            d.mode = (DisplayModeOption)PlayerPrefs.GetInt(K_MODE, (int)d.mode);
            d.width = PlayerPrefs.GetInt(K_W, d.width);
            d.height = PlayerPrefs.GetInt(K_H, d.height);
            d.qualityLevel = PlayerPrefs.GetInt(K_QUALITY, d.qualityLevel);
            d.vsync = PlayerPrefs.GetInt(K_VSYNC, d.vsync ? 1 : 0) == 1;
            d.targetFps = PlayerPrefs.GetInt(K_FPS, d.targetFps);
            return Sanitize(d);
        }

        public static void Save(Data d)
        {
            PlayerPrefs.SetInt(K_MODE, (int)d.mode);
            PlayerPrefs.SetInt(K_W, d.width);
            PlayerPrefs.SetInt(K_H, d.height);
            PlayerPrefs.SetInt(K_QUALITY, d.qualityLevel);
            PlayerPrefs.SetInt(K_VSYNC, d.vsync ? 1 : 0);
            PlayerPrefs.SetInt(K_FPS, d.targetFps);
            PlayerPrefs.SetInt(K_SAVED, 1);
            PlayerPrefs.Save();
        }

        public static void ResetSaved()
        {
            PlayerPrefs.DeleteKey(K_MODE);
            PlayerPrefs.DeleteKey(K_W);
            PlayerPrefs.DeleteKey(K_H);
            PlayerPrefs.DeleteKey(K_QUALITY);
            PlayerPrefs.DeleteKey(K_VSYNC);
            PlayerPrefs.DeleteKey(K_FPS);
            PlayerPrefs.DeleteKey(K_SAVED);
            PlayerPrefs.Save();
        }

        /// <summary>적용. 해상도/모드는 다음 프레임에 반영됨.</summary>
        public static void Apply(Data d)
        {
            d = Sanitize(d);

            // 화질
            if (d.qualityLevel != QualitySettings.GetQualityLevel())
                QualitySettings.SetQualityLevel(d.qualityLevel, true);

            // VSync / FPS  (화질 레벨 변경이 vSyncCount를 덮어쓸 수 있으니 그 뒤에)
            QualitySettings.vSyncCount = d.vsync ? 1 : 0;
            Application.targetFrameRate = d.vsync ? -1 : d.targetFps;

            // 해상도 + 모드
            var mode = ToUnityMode(d.mode);
            if (Screen.width != d.width || Screen.height != d.height || Screen.fullScreenMode != mode)
                Screen.SetResolution(d.width, d.height, mode);

            Debug.Log($"[DisplaySettings] 적용: {d}");
        }

        /// <summary>부팅 시 호출 — 저장값이 있으면 적용, 없으면 기본값(테두리 없는 창) 적용 후 저장.</summary>
        public static void ApplySaved()
        {
            var d = Load();
            Apply(d);
            if (!HasSaved()) Save(d);
        }

        // ---- 해상도 목록 (중복 제거, 큰 순서) ----

        public static List<Vector2Int> GetResolutionOptions(int minWidth = 1024, int minHeight = 576)
        {
            var set = new HashSet<Vector2Int>();
            foreach (var r in Screen.resolutions)
            {
                if (r.width < minWidth || r.height < minHeight) continue;
                set.Add(new Vector2Int(r.width, r.height));
            }
            // 현재/저장 해상도가 목록에 없으면 포함
            set.Add(new Vector2Int(Screen.width, Screen.height));
            var cur = Screen.currentResolution;
            set.Add(new Vector2Int(cur.width, cur.height));

            var list = new List<Vector2Int>(set);
            list.Sort((a, b) =>
            {
                int c = b.x.CompareTo(a.x);
                return c != 0 ? c : b.y.CompareTo(a.y);
            });
            return list;
        }

        public static string ResolutionLabel(Vector2Int r)
        {
            string aspect = AspectLabel(r.x, r.y);
            return string.IsNullOrEmpty(aspect) ? $"{r.x} × {r.y}" : $"{r.x} × {r.y}  ({aspect})";
        }

        public static string AspectLabel(int w, int h)
        {
            float a = (float)w / h;
            if (Mathf.Abs(a - 16f / 9f) < 0.02f) return "16:9";
            if (Mathf.Abs(a - 16f / 10f) < 0.02f) return "16:10";
            if (Mathf.Abs(a - 21f / 9f) < 0.06f) return "21:9";
            if (Mathf.Abs(a - 32f / 9f) < 0.06f) return "32:9";
            if (Mathf.Abs(a - 4f / 3f) < 0.02f) return "4:3";
            if (Mathf.Abs(a - 5f / 4f) < 0.02f) return "5:4";
            return string.Empty;
        }

        public static string ModeLabel(DisplayModeOption m) => m switch
        {
            DisplayModeOption.Fullscreen => "전체화면",
            DisplayModeOption.Borderless => "테두리 없는 창",
            DisplayModeOption.Windowed => "창모드",
            _ => m.ToString()
        };

        public static string FpsLabel(int fps) => fps < 0 ? "무제한" : $"{fps}";

        // ---- 내부 ----

        private static Data Sanitize(Data d)
        {
            int qMax = Mathf.Max(0, QualitySettings.names.Length - 1);
            d.qualityLevel = Mathf.Clamp(d.qualityLevel, 0, qMax);
            if (d.width < 640 || d.height < 360)
            {
                var r = Screen.currentResolution;
                d.width = r.width; d.height = r.height;
            }
            if (d.targetFps == 0) d.targetFps = 60;
            return d;
        }

        public static FullScreenMode ToUnityMode(DisplayModeOption m) => m switch
        {
            DisplayModeOption.Fullscreen => FullScreenMode.ExclusiveFullScreen,
            DisplayModeOption.Borderless => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.Windowed,
        };

        public static DisplayModeOption FromUnityMode(FullScreenMode m) => m switch
        {
            FullScreenMode.ExclusiveFullScreen => DisplayModeOption.Fullscreen,
            FullScreenMode.FullScreenWindow => DisplayModeOption.Borderless,
            FullScreenMode.MaximizedWindow => DisplayModeOption.Borderless,
            _ => DisplayModeOption.Windowed,
        };
    }
}
