using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// CardData → 3D 카드 프리팹.
    ///
    /// 우선순위:
    ///   1) CardDatabase (Addressables) — CardDefinition.prefab3D. 숫자 패 36 + 특수 패 4 모두 여기서.
    ///      프리팹: Assets/3_Prefabs/Cards3D/ (Red_Ele_01 ~ Yellow_Ele_09, Wild, Confusion, Exchange, Serch)
    ///   2) (레거시) Resources/Cards/{Color}_{Theme}_{000} — DB가 아직 없을 때만
    ///
    /// CardGameManager가 시작 시 SetDatabase()로 DB를 넘겨줌.
    /// </summary>
    public static class Card3DResourceMapper
    {
        private static CardDatabase _database;
        private static bool _warnedLegacy;

        public static void SetDatabase(CardDatabase db) => _database = db;

        /// <summary>3D 프리팹. 없으면 null (호출 쪽에서 fallback 사용).</summary>
        public static GameObject LoadPrefab(CardData card)
        {
            if (card == null) return null;

            if (_database != null && card.Definition != null)
            {
                var prefab = _database.GetPrefab(card.Definition);
                if (prefab != null) return prefab;
            }

            // 레거시 경로 (DB 미설정 상태에서 테스트할 때)
            string path = GetPrefabPath(card);
            if (string.IsNullOrEmpty(path)) return null;
            var legacy = Resources.Load<GameObject>(path);
            if (legacy == null && !_warnedLegacy)
            {
                _warnedLegacy = true;
                Debug.LogWarning($"[Card3DResourceMapper] 3D 프리팹 못 찾음 ({card}). CardDatabase가 초기화됐는지 확인하세요.");
            }
            return legacy;
        }

        /// <summary>(레거시) Resources 경로. 예: "Cards/Red_Fire_003"</summary>
        public static string GetPrefabPath(CardData card)
        {
            if (card == null || card.Kind.IsSpecial) return null;
            return $"Cards/{GetPrefabName(card.Kind)}";
        }

        /// <summary>(레거시) CardKind → 옛 파일 이름. 예: Red 3 → "Red_Fire_003"</summary>
        public static string GetPrefabName(CardKind kind)
        {
            if (kind.IsSpecial) return null;
            return $"{GetColorTheme(kind.Color)}_{kind.Number:D3}";
        }

        public static string GetColorTheme(CardColor color) => color switch
        {
            CardColor.Red => "Red_Fire",
            CardColor.Blue => "Blue_Water",
            CardColor.Green => "Green_Air",
            CardColor.Yellow => "Yellow_Earth",
            _ => "Unknown"
        };
    }
}
