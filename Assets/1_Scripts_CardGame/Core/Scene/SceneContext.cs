using System.Collections.Generic;

namespace CardGame.Core
{
    /// <summary>
    /// 씬 간 데이터 전달 (static). DontDestroyOnLoad 없이 사용.
    /// 
    /// 사용 예:
    ///   SceneContext.Set("winnerIndex", 2);
    ///   int winner = SceneContext.Get<int>("winnerIndex", -1);
    ///   SceneContext.Clear();  // 라운드 종료 후 정리
    /// 
    /// 자주 쓰는 데이터는 전용 프로퍼티로 추가.
    /// </summary>
    public static class SceneContext
    {
        private static readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        // 전용 프로퍼티 (자주 쓰는 것)
        public static GameScene PreviousScene { get; set; } = GameScene.TitleScene;
        public static int LastWinnerIndex { get; set; } = -1;
        public static int SelectedPlayerCount { get; set; } = 4;
        public static int SelectedBotDifficulty { get; set; } = 0;

        public static void Set<T>(string key, T value) => _data[key] = value;

        public static T Get<T>(string key, T defaultValue = default)
        {
            if (_data.TryGetValue(key, out var v) && v is T t) return t;
            return defaultValue;
        }

        public static bool Has(string key) => _data.ContainsKey(key);
        public static void Remove(string key) => _data.Remove(key);
        public static void Clear() => _data.Clear();
    }
}
