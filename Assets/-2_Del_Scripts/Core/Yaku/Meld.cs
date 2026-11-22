namespace MahjongAtelier.Core
{
    /// <summary>
    /// 면자의 종류.
    /// </summary>
    public enum MeldType
    {
        /// <summary>슌츠(順子): 같은 수트의 연속 3장 (예: 234m).</summary>
        Sequence,

        /// <summary>코츠(刻子): 같은 패 3장 (예: 555m).</summary>
        Triplet,

        /// <summary>깡즈(槓子): 같은 패 4장. 안깡/명깡 구분은 IsConcealed로.</summary>
        Quad,

        /// <summary>자또우(雀頭/머리/대자): 같은 패 2장.</summary>
        Pair
    }

    /// <summary>
    /// 면자(메르드). 손패를 구성하는 단위.
    /// 슌츠, 코츠, 깡즈, 머리(대자).
    /// 
    /// 후로(鳴き)로 가져온 면자는 IsConcealed=false.
    /// 안깡(暗槓)은 IsConcealed=true이지만 IsCalled=false인 특수 케이스.
    /// 
    /// 표현 방식:
    ///   - 슌츠/코츠/깡즈: BaseKind = 시작 패 (슌츠의 경우 가장 작은 수)
    ///   - 머리: BaseKind = 그 패
    /// </summary>
    public readonly struct Meld
    {
        public readonly MeldType Type;
        public readonly TileKind BaseKind;
        public readonly bool IsConcealed;  // 안깡 또는 멘젠 분해의 면자면 true
        public readonly bool IsCalled;     // 후로로 가져온 면자면 true (치/펑/명깡)

        public Meld(MeldType type, TileKind baseKind, bool isConcealed = true, bool isCalled = false)
        {
            Type = type;
            BaseKind = baseKind;
            IsConcealed = isConcealed;
            IsCalled = isCalled;
        }

        // === 자주 쓰는 판정 ===

        /// <summary>슌츠 여부.</summary>
        public bool IsSequence => Type == MeldType.Sequence;

        /// <summary>코츠 또는 깡즈 (집합적으로 "각자/刻子" 취급).</summary>
        public bool IsTripletOrQuad => Type == MeldType.Triplet || Type == MeldType.Quad;

        public bool IsTriplet => Type == MeldType.Triplet;
        public bool IsQuad => Type == MeldType.Quad;
        public bool IsPair => Type == MeldType.Pair;

        // === 면자에 포함된 패 종류 (역 판정에서 자주 씀) ===

        /// <summary>이 면자에 어떤 종류의 패가 포함되어 있는지.</summary>
        public bool Contains(TileKind kind)
        {
            switch (Type)
            {
                case MeldType.Sequence:
                    return kind.Suit == BaseKind.Suit &&
                           kind.Number >= BaseKind.Number &&
                           kind.Number <= BaseKind.Number + 2;
                case MeldType.Triplet:
                case MeldType.Quad:
                case MeldType.Pair:
                    return kind == BaseKind;
                default:
                    return false;
            }
        }

        /// <summary>면자 내 모든 패가 요구패(1·9·자패)인지. 혼노두/순노두 판정에 사용.</summary>
        public bool AllTerminalOrHonor
        {
            get
            {
                switch (Type)
                {
                    case MeldType.Sequence:
                        return false; // 슌츠는 중장패 포함이라 불가
                    case MeldType.Triplet:
                    case MeldType.Quad:
                    case MeldType.Pair:
                        return BaseKind.IsTerminalOrHonor;
                    default:
                        return false;
                }
            }
        }

        /// <summary>면자에 요구패가 하나라도 포함되어 있는지. 챤타 등 판정에 사용.</summary>
        public bool HasTerminalOrHonor
        {
            get
            {
                switch (Type)
                {
                    case MeldType.Sequence:
                        return BaseKind.Number == 1 || BaseKind.Number == 7;
                    case MeldType.Triplet:
                    case MeldType.Quad:
                    case MeldType.Pair:
                        return BaseKind.IsTerminalOrHonor;
                    default:
                        return false;
                }
            }
        }

        /// <summary>면자 내 모든 패가 노두패(1·9, 자패 제외)인지. 청노두 판정.</summary>
        public bool AllTerminal
        {
            get
            {
                if (Type == MeldType.Sequence) return false;
                return BaseKind.IsTerminal;
            }
        }

        public override string ToString()
        {
            switch (Type)
            {
                case MeldType.Sequence:
                    return FormatSequence();
                case MeldType.Triplet:
                    return $"{BaseKind}{BaseKind}{BaseKind}";
                case MeldType.Quad:
                    return $"{BaseKind}{BaseKind}{BaseKind}{BaseKind}";
                case MeldType.Pair:
                    return $"{BaseKind}{BaseKind}";
                default:
                    return "?";
            }
        }

        private string FormatSequence()
        {
            // 슌츠: 123m 같은 형태. BaseKind가 시작 패.
            var k1 = BaseKind;
            var k2 = new TileKind(BaseKind.Suit, BaseKind.Number + 1);
            var k3 = new TileKind(BaseKind.Suit, BaseKind.Number + 2);
            return $"{k1}{k2}{k3}";
        }
    }
}
