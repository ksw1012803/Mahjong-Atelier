namespace CardGame.Core
{
    /// <summary>
    /// 카드 색상. 기본 4색.
    /// 스킨이 바뀌어도 데이터상 색상은 이 값 그대로.
    /// 예: 판타지 스킨 = 불(Red), 물(Blue), 바람(Green), 땅(Yellow)
    /// </summary>
    public enum CardColor
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3
    }

    /// <summary>
    /// 특수 카드 종류.
    /// </summary>
    public enum SpecialCardType
    {
        None = 0,       // 일반 숫자 카드
        Wild = 1,       // 와일드 - 원하는 카드로 취급
        Swap = 2,       // 교환 - 다른 플레이어와 1장 교환
        Peek = 3,       // 탐색 - 덱 위 3장 확인 후 1장 선택
        Chaos = 4       // 혼란 - 모든 플레이어가 1장씩 왼쪽으로
    }

    /// <summary>
    /// 카드 식별. 게임 로직에서 카드를 구분하는 값형.
    /// 
    /// 일반 카드: Color + Number (1~9)
    /// 특수 카드: Special != None (Color/Number는 무의미)
    /// </summary>
    public readonly struct CardKind : System.IEquatable<CardKind>
    {
        public CardColor Color { get; }
        public int Number { get; }         // 1~9 (특수 카드는 0)
        public SpecialCardType Special { get; }

        public bool IsSpecial => Special != SpecialCardType.None;
        public bool IsNumber => Special == SpecialCardType.None;

        /// <summary>일반 숫자 카드 생성.</summary>
        public CardKind(CardColor color, int number)
        {
            Color = color;
            Number = number;
            Special = SpecialCardType.None;
        }

        /// <summary>특수 카드 생성.</summary>
        public CardKind(SpecialCardType special)
        {
            Color = CardColor.Red;
            Number = 0;
            Special = special;
        }

        public bool Equals(CardKind other) =>
            Color == other.Color && Number == other.Number && Special == other.Special;

        public override bool Equals(object obj) => obj is CardKind k && Equals(k);

        public override int GetHashCode() =>
            ((int)Color) * 1000 + Number * 10 + (int)Special;

        public static bool operator ==(CardKind a, CardKind b) => a.Equals(b);
        public static bool operator !=(CardKind a, CardKind b) => !a.Equals(b);

        public override string ToString()
        {
            if (IsSpecial)
            {
                switch (Special)
                {
                    case SpecialCardType.Wild: return "Wild";
                    case SpecialCardType.Swap: return "Swap";
                    case SpecialCardType.Peek: return "Peek";
                    case SpecialCardType.Chaos: return "Chaos";
                    default: return "?";
                }
            }
            string c = Color switch
            {
                CardColor.Red => "R",
                CardColor.Blue => "B",
                CardColor.Green => "G",
                CardColor.Yellow => "Y",
                _ => "?"
            };
            return $"{c}{Number}";
        }
    }

    /// <summary>
    /// 카드 개별 인스턴스. 각 카드에 고유 ID.
    /// 
    /// 같은 카드 (예: 빨강 3)가 2장 있어도 각각 다른 CardData 인스턴스.
    /// (마작의 TileData와 같은 개념)
    /// </summary>
    public sealed class CardData
    {
        public int Id { get; }
        public CardKind Kind { get; }

        public CardData(int id, CardKind kind)
        {
            Id = id;
            Kind = kind;
        }

        public override string ToString() => Kind.ToString();
    }
}
