using System;
using System.Collections.Generic;
using System.Text;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 손패를 "종류별 개수"로 표현하는 자료구조.
    /// 인덱스 0~33: 만1~만9(0~8), 통1~통9(9~17), 삭1~삭9(18~26), 동남서북(27~30), 백발중(31~33).
    /// 
    /// 마작의 텐파이/화료/역 판정 알고리즘은 거의 모두 이 표현 위에서 동작합니다.
    /// (실제 인스턴스 정보는 점수 계산 시 별도로 결합)
    /// 
    /// 메서드는 모두 in-place로 동작하여 알고리즘 재귀에서 빠르게 사용 가능.
    /// </summary>
    public sealed class TileCounter
    {
        public const int Size = 34;

        private readonly int[] _counts = new int[Size];

        public TileCounter() { }

        public TileCounter(IEnumerable<TileData> tiles)
        {
            foreach (var t in tiles) Add(t.Kind);
        }

        public TileCounter(IEnumerable<TileKind> kinds)
        {
            foreach (var k in kinds) Add(k);
        }

        // === 인덱스 변환 ===

        /// <summary>TileKind → 0~33 인덱스.</summary>
        public static int IndexOf(TileKind kind) => IndexOf(kind.Suit, kind.Number);

        public static int IndexOf(TileSuit suit, int number)
        {
            return suit switch
            {
                TileSuit.Man => number - 1,           // 0~8
                TileSuit.Pin => 9 + (number - 1),     // 9~17
                TileSuit.Sou => 18 + (number - 1),    // 18~26
                TileSuit.Wind => 27 + (number - 1),   // 27~30
                TileSuit.Dragon => 31 + (number - 1), // 31~33
                _ => throw new ArgumentOutOfRangeException(nameof(suit))
            };
        }

        /// <summary>0~33 인덱스 → TileKind.</summary>
        public static TileKind KindOf(int index)
        {
            if (index < 9) return new TileKind(TileSuit.Man, index + 1);
            if (index < 18) return new TileKind(TileSuit.Pin, index - 9 + 1);
            if (index < 27) return new TileKind(TileSuit.Sou, index - 18 + 1);
            if (index < 31) return new TileKind(TileSuit.Wind, index - 27 + 1);
            if (index < 34) return new TileKind(TileSuit.Dragon, index - 31 + 1);
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        // === 접근/수정 ===

        public int this[int index]
        {
            get => _counts[index];
            set => _counts[index] = value;
        }

        public int Get(TileKind kind) => _counts[IndexOf(kind)];

        public void Add(TileKind kind, int amount = 1) => _counts[IndexOf(kind)] += amount;
        public void Remove(TileKind kind, int amount = 1) => _counts[IndexOf(kind)] -= amount;

        public int Total
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < Size; i++) sum += _counts[i];
                return sum;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < Size; i++) _counts[i] = 0;
        }

        public TileCounter Clone()
        {
            var c = new TileCounter();
            Array.Copy(_counts, c._counts, Size);
            return c;
        }

        // === 분석용 헬퍼 (텐파이/역 판정에서 자주 씀) ===

        /// <summary>
        /// 수트별 시작 인덱스. Man=0, Pin=9, Sou=18.
        /// 수패 슈도 알고리즘에서 수트별로 분리해 처리할 때 사용.
        /// </summary>
        public static int SuitStartIndex(TileSuit suit) => suit switch
        {
            TileSuit.Man => 0,
            TileSuit.Pin => 9,
            TileSuit.Sou => 18,
            TileSuit.Wind => 27,
            TileSuit.Dragon => 31,
            _ => -1
        };

        /// <summary>특정 수트의 패만 모두 0개인지.</summary>
        public bool IsSuitEmpty(TileSuit suit)
        {
            int start = SuitStartIndex(suit);
            int len = suit.IsNumber() ? 9 : (suit == TileSuit.Wind ? 4 : 3);
            for (int i = start; i < start + len; i++)
                if (_counts[i] != 0) return false;
            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            // 표준 표기: 1112345678999m 같은 형식
            AppendSuit(sb, TileSuit.Man, 0, 9, 'm');
            AppendSuit(sb, TileSuit.Pin, 9, 9, 'p');
            AppendSuit(sb, TileSuit.Sou, 18, 9, 's');
            // 자패는 한자로
            for (int i = 27; i < 34; i++)
            {
                for (int c = 0; c < _counts[i]; c++)
                    sb.Append(KindOf(i).ToString());
            }
            return sb.Length == 0 ? "(empty)" : sb.ToString();
        }

        private void AppendSuit(StringBuilder sb, TileSuit suit, int start, int len, char letter)
        {
            bool any = false;
            for (int i = start; i < start + len; i++)
            {
                for (int c = 0; c < _counts[i]; c++)
                {
                    sb.Append((i - start) + 1);
                    any = true;
                }
            }
            if (any) sb.Append(letter);
        }
    }
}
