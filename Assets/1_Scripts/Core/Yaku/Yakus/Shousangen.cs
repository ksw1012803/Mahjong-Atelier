namespace MahjongAtelier.Core
{
    /// <summary>
    /// 소삼원 (小三元): 삼원패(백/발/중) 중 2개가 코츠, 1개가 머리. 2판.
    /// 자동으로 역패 2개도 따라오므로 실제로는 최소 4판이 됨.
    /// </summary>
    public sealed class Shousangen : IYaku
    {
        public string Name => "小三元";
        public string NameKr => "소삼원";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            if (decomp.Form != AgariForm.Standard) return false;

            // 머리가 삼원패여야 함
            if (decomp.Pair.BaseKind.Suit != TileSuit.Dragon) return false;

            // 나머지 두 삼원패가 모두 코츠/깡즈여야 함
            var dragonsInTriplet = new System.Collections.Generic.HashSet<TileKind>();
            foreach (var m in DecompositionHelpers.AllMelds(ctx, decomp))
            {
                if (m.IsTripletOrQuad && m.BaseKind.Suit == TileSuit.Dragon)
                    dragonsInTriplet.Add(m.BaseKind);
            }

            // 머리 삼원패와 다른 2종이 코츠로 있어야 함
            int distinctDragons = dragonsInTriplet.Count;
            return distinctDragons == 2 && !dragonsInTriplet.Contains(decomp.Pair.BaseKind);
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 2;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 2;
    }
}
