namespace MahjongAtelier.Core
{
    /// <summary>
    /// 역패 (役牌). 모두 1판. 후로해도 성립.
    /// 종류:
    ///   - 白(白板): 1판
    ///   - 發(綠發): 1판
    ///   - 中(紅中): 1판
    ///   - 자풍(自風): 자기 좌석풍의 코츠 1판
    ///   - 장풍(場風): 현재 장풍의 코츠 1판
    /// 
    /// 동(東)이 자풍이면서 장풍이면 더블 동 → 2판.
    /// 각 역패는 별도 역으로 카운트.
    /// 
    /// 단일 YakuHai 클래스로 각 종류를 표현하기 위해 생성자에서 어떤 패인지 지정.
    /// YakuChecker에서 모든 종류를 등록.
    /// </summary>
    public sealed class YakuHai : IYaku
    {
        public enum HaiType { Dragon, SeatWind, RoundWind }

        private readonly HaiType _type;
        private readonly TileKind _specificDragon; // Dragon 타입일 때만 사용

        public string Name { get; }
        public string NameKr { get; }
        public bool IsYakuman => false;

        // 삼원패 전용
        public YakuHai(TileKind dragonKind)
        {
            _type = HaiType.Dragon;
            _specificDragon = dragonKind;
            Name = dragonKind == TileKind.White ? "白" :
                   dragonKind == TileKind.Green ? "發" : "中";
            NameKr = dragonKind == TileKind.White ? "백" :
                     dragonKind == TileKind.Green ? "발" : "중";
        }

        // 풍패 전용
        public YakuHai(HaiType windType)
        {
            _type = windType;
            Name = windType == HaiType.SeatWind ? "自風" : "場風";
            NameKr = windType == HaiType.SeatWind ? "자풍" : "장풍";
        }

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            TileKind targetKind = _type switch
            {
                HaiType.Dragon => _specificDragon,
                HaiType.SeatWind => ctx.SeatWind,
                HaiType.RoundWind => ctx.RoundWind,
                _ => default
            };

            // 해당 패의 코츠 또는 깡즈가 있는지
            foreach (var m in decomp.Melds)
            {
                if (m.IsTripletOrQuad && m.BaseKind == targetKind)
                    return true;
            }
            // 후로 면자도 검사
            foreach (var m in ctx.CalledMelds)
            {
                if (m.IsTripletOrQuad && m.BaseKind == targetKind)
                    return true;
            }
            return false;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }
}
