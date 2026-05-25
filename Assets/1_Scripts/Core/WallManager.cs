using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 패산(牌山) 관리자.
    /// 
    /// 일본 표준 룰:
    ///   - 총 136장: 수패(34종 × 4) + 자패(7종 × 4) = 34×4
    ///   - 아카도라(빨간 5): 5m, 5p, 5s 각 1장씩이 빨강 (총 3장)
    ///   - 왕패(王牌): 14장은 화료 불가, 그 중 도라/우라도라 표시
    ///   - 가능 쯔모: 136 - 14 = 122장
    /// 
    /// MonoBehaviour 의존 제거 → 단위 테스트 가능.
    /// 셔플은 외부에서 Random을 주입받아 결정론적 테스트 가능.
    /// </summary>
    public sealed class WallManager
    {
        /// <summary>일반 손에서 끌어 쓰는 패산 (live wall).</summary>
        private readonly List<TileData> _liveWall = new List<TileData>();

        /// <summary>왕패 (dead wall). 14장 고정.</summary>
        private readonly List<TileData> _deadWall = new List<TileData>();

        /// <summary>도라 표시패가 뒤집힌 인덱스 (왕패 내). 보통 0부터 시작.</summary>
        private int _doraIndicatorCount = 1;

        public const int TotalTiles = 136;
        public const int DeadWallSize = 14;

        /// <summary>화료 가능 남은 패 수.</summary>
        public int RemainingDrawable => _liveWall.Count;

        public bool IsExhausted => _liveWall.Count == 0;

        /// <summary>
        /// 패산 생성.
        /// </summary>
        /// <param name="includeAkaDora">아카도라 포함 여부 (true: 5m/5p/5s 각 1장이 빨강)</param>
        public void CreateWall(bool includeAkaDora = true)
        {
            _liveWall.Clear();
            _deadWall.Clear();

            int nextId = 0;

            // 수패: 만/통/삭 1~9 각 4장
            foreach (TileSuit suit in new[] { TileSuit.Man, TileSuit.Pin, TileSuit.Sou })
            {
                for (int number = 1; number <= 9; number++)
                {
                    for (int copy = 0; copy < 4; copy++)
                    {
                        // 아카도라: 5의 첫 번째 카피(copy==0)를 빨강으로
                        bool isRed = includeAkaDora && number == 5 && copy == 0;
                        var kind = new TileKind(suit, number);
                        _liveWall.Add(new TileData(nextId++, kind, isRed));
                    }
                }
            }

            // 풍패: 동남서북 각 4장
            for (int number = 1; number <= 4; number++)
            {
                for (int copy = 0; copy < 4; copy++)
                    _liveWall.Add(new TileData(nextId++, new TileKind(TileSuit.Wind, number)));
            }

            // 삼원패: 백발중 각 4장
            for (int number = 1; number <= 3; number++)
            {
                for (int copy = 0; copy < 4; copy++)
                    _liveWall.Add(new TileData(nextId++, new TileKind(TileSuit.Dragon, number)));
            }

            GameEvents.RaiseWallCreated();
            GameEvents.RaiseWallCountChanged(_liveWall.Count);
        }

        /// <summary>
        /// 셔플. System.Random을 주입받아 결정론적 테스트 가능.
        /// Unity에서는 new System.Random()을 그대로 사용.
        /// </summary>
        public void Shuffle(System.Random rng)
        {
            // Fisher-Yates
            for (int i = _liveWall.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_liveWall[i], _liveWall[j]) = (_liveWall[j], _liveWall[i]);
            }

            // 셔플 후 왕패 14장 분리
            SetupDeadWall();
        }

        private void SetupDeadWall()
        {
            _deadWall.Clear();
            int splitFrom = _liveWall.Count - DeadWallSize;
            for (int i = splitFrom; i < _liveWall.Count; i++)
                _deadWall.Add(_liveWall[i]);
            _liveWall.RemoveRange(splitFrom, DeadWallSize);
        }

        /// <summary>패산에서 1장 쯔모.</summary>
        public TileData DrawTile()
        {
            if (_liveWall.Count == 0) return null;

            // 마지막에서 빼는 것이 O(1)
            int lastIndex = _liveWall.Count - 1;
            TileData tile = _liveWall[lastIndex];
            _liveWall.RemoveAt(lastIndex);

            GameEvents.RaiseWallCountChanged(_liveWall.Count);
            return tile;
        }

        // === 도라 ===

        /// <summary>현재 표시된 도라 인디케이터들.</summary>
        public IReadOnlyList<TileData> GetDoraIndicators()
        {
            var list = new List<TileData>();
            for (int i = 0; i < _doraIndicatorCount && i < _deadWall.Count; i++)
                list.Add(_deadWall[i]);
            return list;
        }

        /// <summary>깡 등으로 도라 추가.</summary>
        public void RevealAdditionalDora()
        {
            if (_doraIndicatorCount < 5) _doraIndicatorCount++;
        }

        /// <summary>도라 표시패 → 도라패 변환. (예: 1m 표시 → 2m이 도라, 9m 표시 → 1m이 도라)</summary>
        public static TileKind IndicatorToDora(TileKind indicator)
        {
            if (indicator.Suit.IsNumber())
            {
                int next = indicator.Number == 9 ? 1 : indicator.Number + 1;
                return new TileKind(indicator.Suit, next);
            }
            if (indicator.Suit == TileSuit.Wind)
            {
                // 東→南→西→北→東
                int next = indicator.Number == 4 ? 1 : indicator.Number + 1;
                return new TileKind(TileSuit.Wind, next);
            }
            if (indicator.Suit == TileSuit.Dragon)
            {
                // 白→發→中→白
                int next = indicator.Number == 3 ? 1 : indicator.Number + 1;
                return new TileKind(TileSuit.Dragon, next);
            }
            return indicator;
        }
    }
}
