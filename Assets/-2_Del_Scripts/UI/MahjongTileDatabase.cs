using System.Collections.Generic;
using UnityEngine;
using MahjongAtelier.Core;

namespace MahjongAtelier.UI
{
    /// <summary>
    /// 패 스프라이트 데이터베이스.
    /// 스프라이트 이름 규칙: "{Suit}_{Number}"  (예: Man_5, Wind_1, Dragon_3)
    /// 아카도라용: "{Suit}_{Number}_Red"       (예: Man_5_Red, Pin_5_Red, Sou_5_Red)
    /// 아카도라용 스프라이트가 없으면 일반 스프라이트로 폴백.
    /// </summary>
    public class MahjongTileDatabase : MonoBehaviour
    {
        [Header("모든 패 스프라이트")]
        public Sprite[] allSprites;

        private readonly Dictionary<string, Sprite> spriteDict = new Dictionary<string, Sprite>();

        private void Awake()
        {
            BuildDictionary();
        }

        private void BuildDictionary()
        {
            spriteDict.Clear();
            foreach (Sprite sprite in allSprites)
            {
                if (sprite == null) continue;
                if (!spriteDict.ContainsKey(sprite.name))
                    spriteDict.Add(sprite.name, sprite);
                else
                    Debug.LogWarning($"중복 스프라이트 이름: {sprite.name}");
            }
            Debug.Log($"패 스프라이트 로드 완료: {spriteDict.Count}개");

            // ↓ 추가: 등록된 모든 키를 한 줄로 출력
            Debug.Log($"[디버그] 등록된 키: {string.Join(", ", spriteDict.Keys)}");
        }

        public Sprite GetSprite(TileData tile)
        {
            // 1) 아카도라이면 _Red 스프라이트 우선
            if (tile.IsRedFive)
            {
                string redKey = tile.ToResourceKey(); // 이미 _Red 포함
                if (spriteDict.TryGetValue(redKey, out var red)) return red;
                // 폴백: 일반 5
            }

            string key = tile.Kind.ToResourceKey();
            if (spriteDict.TryGetValue(key, out var sprite)) return sprite;

            Debug.LogWarning($"스프라이트 없음: {key} (tile={tile})");
            return null;
        }

        public Sprite GetSprite(TileKind kind)
        {
            string key = kind.ToResourceKey();
            if (spriteDict.TryGetValue(key, out var sprite)) return sprite;
            Debug.LogWarning($"스프라이트 없음: {key}");
            return null;
        }
    }
}
