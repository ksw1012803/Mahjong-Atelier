using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// CardData → 3D 프리팹 이름 매핑.
    /// 
    /// 파일 이름 규칙: {Color}_{Theme}_{Number:000}
    /// - Red_Fire_001 ~ Red_Fire_009
    /// - Blue_Water_001 ~ Blue_Water_009
    /// - Green_Air_001 ~ Green_Air_009
    /// - Yellow_Earth_001 ~ Yellow_Earth_009
    /// 
    /// Resources 폴더에 있어야 Resources.Load로 접근 가능.
    /// </summary>
    public static class Card3DResourceMapper
    {
        /// <summary>
        /// CardData → 3D 프리팹 리소스 경로 (Resources 폴더 기준).
        /// 예: "Cards/Red_Fire_003"
        /// </summary>
        public static string GetPrefabPath(CardData card)
        {
            if (card == null || card.Kind.IsSpecial) return null;
            return $"Cards/{GetPrefabName(card.Kind)}";
        }

        /// <summary>
        /// CardKind → 파일 이름 (확장자 없음).
        /// 예: Red 3 → "Red_Fire_003"
        /// </summary>
        public static string GetPrefabName(CardKind kind)
        {
            if (kind.IsSpecial) return null;
            string colorTheme = GetColorTheme(kind.Color);
            return $"{colorTheme}_{kind.Number:D3}";
        }

        /// <summary>색 → 테마 이름.</summary>
        public static string GetColorTheme(CardColor color) => color switch
        {
            CardColor.Red => "Red_Fire",
            CardColor.Blue => "Blue_Water",
            CardColor.Green => "Green_Air",
            CardColor.Yellow => "Yellow_Earth",
            _ => "Unknown"
        };

        /// <summary>
        /// 3D 프리팹 로드 (Resources.Load).
        /// null 반환 시 프리팹 없음 (Wild 카드 등 미제작 카드).
        /// </summary>
        public static GameObject LoadPrefab(CardData card)
        {
            string path = GetPrefabPath(card);
            if (string.IsNullOrEmpty(path)) return null;

            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"[Card3DResourceMapper] 프리팹 못 찾음: {path}");
            }
            return prefab;
        }
    }
}
