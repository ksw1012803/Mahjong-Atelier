using System;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 패의 "종류"를 나타내는 불변 값 객체.
    /// (Suit, Number) 조합으로 패의 정체성을 결정합니다.
    ///
    /// TileData(개별 인스턴스)와의 차이:
    ///   - TileKind: "1만" 같은 종류. 34가지 존재 (수패 27 + 풍패 4 + 삼원패 3).
    ///   - TileData: 그 종류의 구체적 인스턴스. 총 136장이 존재 (각 종류당 4장).
    ///
    /// 텐파이 판정/역 판정에서는 TileKind만 알면 충분합니다 (어느 1만인지는 의미 없음).
    /// 아카도라/도라 판정 시에는 TileData가 필요합니다.
    /// </summary>
    public readonly struct TileKind : IEquatable<TileKind>, IComparable<TileKind>
    {
        public readonly TileSuit Suit;
        public readonly int Number;

        public TileKind(TileSuit suit, int number)
        {
            ValidateRange(suit, number);
            Suit = suit;
            Number = number;
        }

        private static void ValidateRange(TileSuit suit, int number)
        {
            int max = suit switch
            {
                TileSuit.Man or TileSuit.Pin or TileSuit.Sou => 9,
                TileSuit.Wind => 4,
                TileSuit.Dragon => 3,
                _ => 0
            };
            if (number < 1 || number > max)
                throw new ArgumentOutOfRangeException(nameof(number),
                    $"{suit}의 number 범위는 1~{max}, 입력값: {number}");
        }

        // === 자주 쓰는 판정 ===

        /// <summary>요구패(么九牌): 수패의 1·9 + 모든 자패. 단요구/혼노두 등 역 판정에 사용.</summary>
        public bool IsTerminalOrHonor =>
            Suit.IsHonor() || Number == 1 || Number == 9;

        /// <summary>노두패(老頭牌): 수패의 1·9만. 청노두 등에 사용.</summary>
        public bool IsTerminal =>
            Suit.IsNumber() && (Number == 1 || Number == 9);

        public bool IsHonor => Suit.IsHonor();
        public bool IsNumber => Suit.IsNumber();

        /// <summary>중장패(中張牌): 수패의 2~8. 단요구의 핵심 조건.</summary>
        public bool IsSimple =>
            Suit.IsNumber() && Number >= 2 && Number <= 8;

        // === 정렬 ===
        // 만1, 만2, ..., 만9, 통1, ..., 삭9, 동, 남, 서, 북, 백, 발, 중
        public int SortOrder => (int)Suit * 10 + Number;

        public int CompareTo(TileKind other) => SortOrder.CompareTo(other.SortOrder);

        // === 동등성 (struct 기본을 명시적으로 구현하여 박싱 회피) ===

        public bool Equals(TileKind other) => Suit == other.Suit && Number == other.Number;
        public override bool Equals(object obj) => obj is TileKind other && Equals(other);
        public override int GetHashCode() => ((int)Suit * 10) + Number;

        public static bool operator ==(TileKind a, TileKind b) => a.Equals(b);
        public static bool operator !=(TileKind a, TileKind b) => !a.Equals(b);

        // === 표시 ===

        public override string ToString()
        {
            return Suit switch
            {
                TileSuit.Man => $"{Number}m",
                TileSuit.Pin => $"{Number}p",
                TileSuit.Sou => $"{Number}s",
                TileSuit.Wind => Number switch
                {
                    1 => "東", 2 => "南", 3 => "西", 4 => "北", _ => "?"
                },
                TileSuit.Dragon => Number switch
                {
                    1 => "白", 2 => "發", 3 => "中", _ => "?"
                },
                _ => "??"
            };
        }

        /// <summary>스프라이트/리소스 키 생성용. 영문 안정 키.</summary>
        public string ToResourceKey() => $"{Suit}_{Number}";

        // === 자주 쓰는 정적 헬퍼 ===

        public static TileKind Man(int n) => new TileKind(TileSuit.Man, n);
        public static TileKind Pin(int n) => new TileKind(TileSuit.Pin, n);
        public static TileKind Sou(int n) => new TileKind(TileSuit.Sou, n);
        public static TileKind East  => new TileKind(TileSuit.Wind, 1);
        public static TileKind South => new TileKind(TileSuit.Wind, 2);
        public static TileKind West  => new TileKind(TileSuit.Wind, 3);
        public static TileKind North => new TileKind(TileSuit.Wind, 4);
        public static TileKind White => new TileKind(TileSuit.Dragon, 1); // 白
        public static TileKind Green => new TileKind(TileSuit.Dragon, 2); // 發
        public static TileKind Red   => new TileKind(TileSuit.Dragon, 3); // 中
    }
}
