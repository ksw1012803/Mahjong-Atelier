using System;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 패의 구체적 인스턴스. 패산의 136장 각각이 고유한 TileData입니다.
    /// 
    /// 같은 1만이라도 4장이 모두 다른 TileData (Id 다름).
    /// 아카도라(빨간 5)는 같은 5m이라도 IsRedFive=true로 구별됩니다.
    /// 
    /// 동등성:
    ///   - Equals/GetHashCode는 Id 기준 (인스턴스 동일성).
    ///   - 종류 비교는 Kind 프로퍼티로: tile1.Kind == tile2.Kind
    /// </summary>
    public sealed class TileData : IEquatable<TileData>
    {
        /// <summary>패산 내 고유 ID (0~135). 같은 종류라도 다른 ID.</summary>
        public int Id { get; }

        /// <summary>패의 종류 (수트 + 숫자). 텐파이/역 판정에 사용.</summary>
        public TileKind Kind { get; }

        /// <summary>아카도라(빨간 5) 여부. 일본 표준 룰: 5m/5p/5s에 각 1장씩.</summary>
        public bool IsRedFive { get; }

        public TileSuit Suit => Kind.Suit;
        public int Number => Kind.Number;

        public TileData(int id, TileKind kind, bool isRedFive = false)
        {
            if (isRedFive && !IsValidRedFiveSlot(kind))
                throw new ArgumentException(
                    $"아카도라는 수패의 5에만 가능합니다. 입력: {kind}");

            Id = id;
            Kind = kind;
            IsRedFive = isRedFive;
        }

        private static bool IsValidRedFiveSlot(TileKind kind) =>
            kind.Suit.IsNumber() && kind.Number == 5;

        // === 동등성: 인스턴스 동일성(Id 기준) ===
        // 종류 비교는 Kind를 통해 명시적으로 하세요: a.Kind == b.Kind
        public bool Equals(TileData other) =>
            other != null && Id == other.Id;

        public override bool Equals(object obj) => Equals(obj as TileData);
        public override int GetHashCode() => Id;

        public static bool operator ==(TileData a, TileData b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a is null || b is null) return false;
            return a.Equals(b);
        }
        public static bool operator !=(TileData a, TileData b) => !(a == b);

        public override string ToString() =>
            IsRedFive ? $"赤{Kind}" : Kind.ToString();

        /// <summary>스프라이트 키. 아카도라는 별도 스프라이트가 필요하면 _Red 접미사.</summary>
        public string ToResourceKey() =>
            IsRedFive ? $"{Kind.ToResourceKey()}_Red" : Kind.ToResourceKey();
    }
}
