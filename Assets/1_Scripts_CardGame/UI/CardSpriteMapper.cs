using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// CardData → 2D 스프라이트 매핑.
    /// 
    /// ⚠️ TEMPORARY: 전달 오류 대응용 임시 네이밍.
    /// 파일 이름 규칙: {theme}_{color}_card_{number:00} (소문자, 테마가 앞)
    /// - fire_red_card_01 ~ fire_red_card_09
    /// - water_blue_card_01 ~ water_blue_card_09
    /// - air_green_card_01 ~ air_green_card_09
    /// - earth_yellow_card_01 ~ earth_yellow_card_09
    /// 
    /// 위치: Assets/Resources/CardSprites/
    /// 
    /// TODO: 정식 네이밍 받으면 GetSpriteName() 수정.
    /// </summary>
    public static class CardSpriteMapper
    {
        private static readonly System.Collections.Generic.Dictionary<string, Sprite> _spriteCache
            = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>
        /// CardData → 스프라이트 리소스 경로 (Resources 폴더 기준).
        /// 예: "CardSprites/water_blue_card_03"
        /// </summary>
        public static string GetSpritePath(CardData card)
        {
            if (card == null || card.Kind.IsSpecial) return null;
            return $"CardSprites/{GetSpriteName(card.Kind)}";
        }

        /// <summary>
        /// CardKind → 파일 이름 (확장자 없음).
        /// 예: Red 3 → "fire_red_card_03"
        /// </summary>
        public static string GetSpriteName(CardKind kind)
        {
            if (kind.IsSpecial) return null;

            // ⚠️ TEMPORARY NAMING (전달 오류 임시 대응)
            // 정식: {Color}_{Theme}_Card_{Number:D3}  (예: Red_Fire_Card_003)
            // 임시: {theme}_{color}_card_{Number:D2}  (예: fire_red_card_03)
            string themeColor = GetThemeColorLowercase(kind.Color);
            return $"{themeColor}_card_{kind.Number:D2}";
        }

        /// <summary>색 → "테마_색상" 소문자 (임시 네이밍).</summary>
        private static string GetThemeColorLowercase(CardColor color) => color switch
        {
            CardColor.Red => "fire_red",
            CardColor.Blue => "water_blue",
            CardColor.Green => "wind_green",
            CardColor.Yellow => "earth_yellow",
            _ => "unknown"
        };

        /// <summary>색 → 테마 이름 (기존 네이밍, 3D 매퍼 호환용).</summary>
        public static string GetColorTheme(CardColor color) => color switch
        {
            CardColor.Red => "Red_Fire",
            CardColor.Blue => "Blue_Water",
            CardColor.Green => "Green_Air",
            CardColor.Yellow => "Yellow_Earth",
            _ => "Unknown"
        };

        /// <summary>
        /// 스프라이트 로드 (캐시 사용).
        /// null 반환 시 스프라이트 없음 (Wild 등 미제작).
        /// </summary>
        public static Sprite LoadSprite(CardData card)
        {
            if (card == null) return null;
            string path = GetSpritePath(card);
            if (string.IsNullOrEmpty(path)) return null;

            if (_spriteCache.TryGetValue(path, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"[CardSpriteMapper] 스프라이트 못 찾음: {path}");
            }
            else
            {
                _spriteCache[path] = sprite;
            }
            return sprite;
        }

        /// <summary>캐시 비우기 (리소스 변경 시).</summary>
        public static void ClearCache()
        {
            _spriteCache.Clear();
        }
    }
}