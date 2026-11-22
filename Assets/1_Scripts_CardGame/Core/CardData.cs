namespace CardGame.Core
{
    /// <summary>
    /// 카드 인스턴스 v2 — 네트워크 식별자(InstanceId) 추가.
    ///
    /// v2 변경:
    ///   - InstanceId: 덱 생성 시 0..N-1 순차 부여. 같은 Definition이 2장씩 있어도 구분 가능.
    ///                 온라인에서 호스트↔클라이언트가 "어느 카드"인지 가리키는 유일한 키.
    ///   - IsPlaceholder: 온라인 클라이언트에서 "상대 손패 자리표시용" 더미 카드 (내용 모름, 장수만 맞춤).
    /// </summary>
    public class CardData
    {
        public CardKind Kind { get; }
        public CardDefinition Definition { get; }

        /// <summary>덱 생성 시 부여되는 고유 번호. -1 = 미부여(레거시/플레이스홀더).</summary>
        public int InstanceId { get; set; } = -1;

        /// <summary>온라인 클라이언트에서 상대 손패를 장수만 표현하기 위한 더미 여부.</summary>
        public bool IsPlaceholder { get; }

        public CardData(CardDefinition definition)
        {
            Definition = definition;
            Kind = definition != null ? definition.ToKind() : default;
            IsPlaceholder = false;
        }

        /// <summary>레거시 생성자 (Definition 없이도 사용 가능).</summary>
        public CardData(CardKind kind)
        {
            Kind = kind;
            Definition = null;
            IsPlaceholder = false;
        }

        private CardData(bool placeholder)
        {
            Kind = default;
            Definition = null;
            IsPlaceholder = placeholder;
        }

        /// <summary>상대 손패 자리표시용 더미 카드 생성.</summary>
        public static CardData CreatePlaceholder() => new CardData(true);

        public override string ToString()
        {
            if (IsPlaceholder) return "[?]";
            if (Kind.IsSpecial) return $"[{Kind.Special}]";
            string c = Kind.Color switch
            {
                CardColor.Red => "R",
                CardColor.Blue => "B",
                CardColor.Green => "G",
                CardColor.Yellow => "Y",
                _ => "?"
            };
            return $"{c}{Kind.Number}";
        }
    }
}
