using System.Collections.Generic;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 패산 매니저 v4 — 깡 처리 (린샨 패 + 추가 도라 공개) 추가.
    /// 
    /// 왕패(14장) 구조:
    ///   인덱스 0-4: 도라 표시패 (위, 처음엔 1장만 공개)
    ///   인덱스 5-9: 우라도라 표시패 (아래)
    ///   인덱스 10-13: 깡 보충 패 (린샨패), 깡마다 1장씩 소비
    /// 
    /// 깡 발생 시:
    ///   1) 린샨패 1장 소비 → 화료자에게 (DrawRinshan)
    ///   2) 새 도라 표시패 1장 공개 (RevealAdditionalDora)
    /// </summary>
    public sealed class WallManager
    {
        private readonly List<TileData> _liveWall = new List<TileData>();
        private readonly List<TileData> _deadWall = new List<TileData>();

        private int _doraIndicatorCount = 1;
        private int _kanCount = 0;

        public const int TotalTiles = 136;
        public const int DeadWallSize = 14;
        public const int MaxKans = 4;

        public int RemainingDrawable => _liveWall.Count;
        public bool IsExhausted => _liveWall.Count == 0;
        public int KanCount => _kanCount;
        public bool CanDrawRinshan => _kanCount < MaxKans;

        public void CreateWall(bool includeAkaDora = true)
        {
            _liveWall.Clear();
            _deadWall.Clear();
            _doraIndicatorCount = 1;
            _kanCount = 0;

            int nextId = 0;

            foreach (TileSuit suit in new[] { TileSuit.Man, TileSuit.Pin, TileSuit.Sou })
            {
                for (int number = 1; number <= 9; number++)
                {
                    for (int copy = 0; copy < 4; copy++)
                    {
                        bool isRed = includeAkaDora && number == 5 && copy == 0;
                        var kind = new TileKind(suit, number);
                        _liveWall.Add(new TileData(nextId++, kind, isRed));
                    }
                }
            }

            for (int number = 1; number <= 4; number++)
            {
                for (int copy = 0; copy < 4; copy++)
                    _liveWall.Add(new TileData(nextId++, new TileKind(TileSuit.Wind, number)));
            }

            for (int number = 1; number <= 3; number++)
            {
                for (int copy = 0; copy < 4; copy++)
                    _liveWall.Add(new TileData(nextId++, new TileKind(TileSuit.Dragon, number)));
            }

            GameEvents.RaiseWallCreated();
            GameEvents.RaiseWallCountChanged(_liveWall.Count);
        }

        public void Shuffle(System.Random rng)
        {
            for (int i = _liveWall.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (_liveWall[i], _liveWall[j]) = (_liveWall[j], _liveWall[i]);
            }
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

        public TileData DrawTile()
        {
            if (_liveWall.Count == 0) return null;
            int lastIndex = _liveWall.Count - 1;
            TileData tile = _liveWall[lastIndex];
            _liveWall.RemoveAt(lastIndex);
            GameEvents.RaiseWallCountChanged(_liveWall.Count);
            return tile;
        }

        // === 깡 처리 ===

        /// <summary>
        /// 린샨 패(嶺上牌) 1장을 가져옴. 깡 후 보충용.
        /// 동시에 _kanCount 증가.
        /// </summary>
        public TileData DrawRinshan()
        {
            if (!CanDrawRinshan) return null;

            // 린샨패 영역: dead wall 인덱스 10~13 (4장)
            // 깡 횟수에 따라 13, 12, 11, 10번 순서
            int rinshanIdx = 13 - _kanCount;
            if (rinshanIdx < 10 || rinshanIdx >= _deadWall.Count) return null;

            var tile = _deadWall[rinshanIdx];
            _kanCount++;

            // 라이브 월에서 끝에서 1장을 deadwall로 이동 (실제 마작 룰)
            // 단순화: 그냥 _liveWall 끝을 1장 줄여서 패산 수 맞춤
            if (_liveWall.Count > 0)
            {
                // 패산 수는 그대로 줄어든 것처럼 처리 (실제로는 라이브월에서 1장 deadwall로 이동)
                // 여기서는 단순화: 그냥 1장 줄임
                _liveWall.RemoveAt(_liveWall.Count - 1);
            }

            GameEvents.RaiseWallCountChanged(_liveWall.Count);
            return tile;
        }

        // === 도라 ===

        public IReadOnlyList<TileData> GetDoraIndicators()
        {
            var list = new List<TileData>();
            for (int i = 0; i < _doraIndicatorCount && i < 5 && i < _deadWall.Count; i++)
                list.Add(_deadWall[i]);
            return list;
        }

        public IReadOnlyList<TileData> GetUraDoraIndicators()
        {
            var list = new List<TileData>();
            for (int i = 0; i < _doraIndicatorCount && i < 5; i++)
            {
                int idx = 5 + i;
                if (idx < _deadWall.Count) list.Add(_deadWall[idx]);
            }
            return list;
        }

        /// <summary>새 도라 표시패 공개 (깡 시).</summary>
        public void RevealAdditionalDora()
        {
            if (_doraIndicatorCount < 5) _doraIndicatorCount++;
        }

        public static TileKind IndicatorToDora(TileKind indicator)
        {
            if (indicator.Suit.IsNumber())
            {
                int next = indicator.Number == 9 ? 1 : indicator.Number + 1;
                return new TileKind(indicator.Suit, next);
            }
            if (indicator.Suit == TileSuit.Wind)
            {
                int next = indicator.Number == 4 ? 1 : indicator.Number + 1;
                return new TileKind(TileSuit.Wind, next);
            }
            if (indicator.Suit == TileSuit.Dragon)
            {
                int next = indicator.Number == 3 ? 1 : indicator.Number + 1;
                return new TileKind(TileSuit.Dragon, next);
            }
            return indicator;
        }
    }
}
