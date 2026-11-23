using System.Collections.Generic;
using UnityEngine;

namespace CardGame.Core
{
    /// <summary>
    /// 선택 가능한 캐릭터 목록. Resources/CharacterDatabase.asset 하나만 둔다.
    /// 어느 씬에서든 CharacterDatabase.Instance로 접근 (씬마다 연결할 필요 없음).
    /// 캐릭터는 배열 순서(index)로 저장·동기화되므로, 순서를 바꾸면 저장된 선택이 다른 캐릭터를 가리킨다.
    /// 새 캐릭터는 배열 끝에 추가할 것.
    /// </summary>
    [CreateAssetMenu(menuName = "SEVEN MAID/Character Database", fileName = "CharacterDatabase")]
    public class CharacterDatabase : ScriptableObject
    {
        public const string ResourcePath = "CharacterDatabase";

        [SerializeField] private CharacterDefinition[] characters = new CharacterDefinition[0];

        public int Count => characters != null ? characters.Length : 0;
        public IReadOnlyList<CharacterDefinition> All => characters;

        private static CharacterDatabase _instance;
        public static CharacterDatabase Instance
        {
            get
            {
                if (_instance == null) _instance = Resources.Load<CharacterDatabase>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>index의 캐릭터. 범위를 벗어나면 순환. 목록이 비었으면 null.</summary>
        public CharacterDefinition Get(int index)
        {
            if (Count == 0) return null;
            int i = index % Count;
            if (i < 0) i += Count;
            return characters[i];
        }

        public int ClampIndex(int index) => Count == 0 ? 0 : Mathf.Clamp(index, 0, Count - 1);

        /// <summary>
        /// 봇에게 줄 캐릭터를 랜덤으로 고름 — 사람들이 고른 캐릭터·다른 봇과 겹치지 않게.
        /// 남은 캐릭터가 모자라면 그때만 겹치게 배정.
        /// 온라인에서는 호스트·클라이언트가 같은 seed를 넘기면 같은 결과 (rng 공유).
        /// </summary>
        public static int[] PickBotCharacters(IEnumerable<int> humanCharacters, int botCount, System.Random rng = null)
        {
            var result = new int[botCount];
            int count = Instance != null ? Instance.Count : 0;
            if (count == 0 || botCount == 0) return result;
            if (rng == null) rng = new System.Random();

            var used = new HashSet<int>();
            if (humanCharacters != null) foreach (var c in humanCharacters) used.Add(c);

            // 안 겹치는 후보를 섞어서 앞에서부터 배정
            var pool = new List<int>();
            for (int i = 0; i < count; i++) if (!used.Contains(i)) pool.Add(i);
            Shuffle(pool, rng);

            for (int b = 0; b < botCount; b++)
            {
                if (pool.Count == 0)
                {
                    // 캐릭터 수보다 사람+봇이 많음 → 전체에서 다시 섞어 채움
                    for (int i = 0; i < count; i++) pool.Add(i);
                    Shuffle(pool, rng);
                }
                result[b] = pool[pool.Count - 1];
                pool.RemoveAt(pool.Count - 1);
            }
            return result;
        }

        private static void Shuffle(List<int> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}
