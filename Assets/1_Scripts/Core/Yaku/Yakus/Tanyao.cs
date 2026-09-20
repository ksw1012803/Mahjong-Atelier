using UnityEngine;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 탄야오 (断幺九) — 디버그 로그 추가.
    /// </summary>
    public sealed class Tanyao : IYaku
    {
        public string Name => "断幺九";
        public string NameKr => "탕야오";
        public bool IsYakuman => false;

        public bool IsApplicable(YakuContext ctx, HandDecomposition decomp)
        {
            var hand = ctx.Hand;

            // 손패의 1/9/자패 검사
            if (hand[0] > 0) { Debug.Log($"[Tanyao] 손패에 1m {hand[0]}장 → 탕야오 불가"); return false; }
            if (hand[8] > 0) { Debug.Log($"[Tanyao] 손패에 9m {hand[8]}장 → 탕야오 불가"); return false; }
            if (hand[9] > 0) { Debug.Log($"[Tanyao] 손패에 1p {hand[9]}장 → 탕야오 불가"); return false; }
            if (hand[17] > 0) { Debug.Log($"[Tanyao] 손패에 9p {hand[17]}장 → 탕야오 불가"); return false; }
            if (hand[18] > 0) { Debug.Log($"[Tanyao] 손패에 1s {hand[18]}장 → 탕야오 불가"); return false; }
            if (hand[26] > 0) { Debug.Log($"[Tanyao] 손패에 9s {hand[26]}장 → 탕야오 불가"); return false; }
            for (int i = 27; i < 34; i++)
            {
                if (hand[i] > 0)
                {
                    Debug.Log($"[Tanyao] 손패에 자패 idx={i} {hand[i]}장 → 탕야오 불가");
                    return false;
                }
            }

            // 후로 면자도 검사 (★ 원래 코드에 누락됨)
            if (ctx.CalledMelds != null)
            {
                foreach (var meld in ctx.CalledMelds)
                {
                    if (MeldHasTerminalOrHonor(meld))
                    {
                        Debug.Log($"[Tanyao] 후로 {meld.Type} {meld.BaseKind}에 1/9/자패 포함 → 탕야오 불가");
                        return false;
                    }
                }
            }

            Debug.Log($"[Tanyao] 모든 검사 통과 → 탕야오 성립!");
            return true;
        }

        private bool MeldHasTerminalOrHonor(Meld meld)
        {
            var k = meld.BaseKind;
            // 자패면 무조건
            if (k.Suit == TileSuit.Wind || k.Suit == TileSuit.Dragon) return true;

            // 슌츠 (Sequence): BaseKind ~ BaseKind+2 중에 1/9 포함되는지
            if (meld.Type == MeldType.Sequence)
            {
                // 슌츠 시작이 1이면 1-2-3 (1 포함), 시작이 7이면 7-8-9 (9 포함)
                if (k.Number == 1 || k.Number == 7) return true;
                return false;
            }

            // 코츠/깡즈: BaseKind가 1 또는 9면 안 됨
            if (k.Number == 1 || k.Number == 9) return true;
            return false;
        }

        public int GetHan(YakuContext ctx, HandDecomposition decomp) => 1;
        public int GetHanCalled(YakuContext ctx, HandDecomposition decomp) => 1;
    }
}