namespace CardGame.Core
{
    /// <summary>카드 색상 (4종).</summary>
    public enum CardColor
    {
        Red,
        Blue,
        Green,
        Yellow,
    }

    /// <summary>특수 카드 종류 (4종).</summary>
    public enum SpecialCardType
    {
        Wild,
        Swap,
        Peek,
        Chaos,
    }

    /// <summary>
    /// 카드 종류 (불변 값 타입).
    /// 호환성 유지:
    ///   - new CardKind(color, number)  → 일반 카드
    ///   - new CardKind(special)        → 특수 카드
    ///   - 또는 팩토리 메서드 MakeNormal/MakeSpecial
    /// </summary>
    public readonly struct CardKind
    {
        public bool IsSpecial { get; }
        public CardColor Color { get; }
        public int Number { get; }
        public SpecialCardType Special { get; }

        /// <summary>일반 카드 생성자 (color, number).</summary>
        public CardKind(CardColor color, int number)
        {
            IsSpecial = false;
            Color = color;
            Number = number < 1 ? 1 : (number > 9 ? 9 : number);
            Special = SpecialCardType.Wild;
        }

        /// <summary>특수 카드 생성자 (SpecialCardType).</summary>
        public CardKind(SpecialCardType type)
        {
            IsSpecial = true;
            Color = CardColor.Red;
            Number = 0;
            Special = type;
        }

        /// <summary>일반 카드 팩토리 (명시적).</summary>
        public static CardKind MakeNormal(CardColor color, int number) => new CardKind(color, number);

        /// <summary>특수 카드 팩토리 (명시적).</summary>
        public static CardKind MakeSpecial(SpecialCardType type) => new CardKind(type);

        public override bool Equals(object obj) => obj is CardKind other && Equals(other);
        public bool Equals(CardKind other)
        {
            if (IsSpecial != other.IsSpecial) return false;
            if (IsSpecial) return Special == other.Special;
            return Color == other.Color && Number == other.Number;
        }

        public override int GetHashCode()
        {
            if (IsSpecial) return (int)Special * 1000 + 999;
            return (int)Color * 10 + Number;
        }

        public static bool operator ==(CardKind a, CardKind b) => a.Equals(b);
        public static bool operator !=(CardKind a, CardKind b) => !a.Equals(b);

        public override string ToString()
        {
            if (IsSpecial) return $"[{Special}]";
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
}