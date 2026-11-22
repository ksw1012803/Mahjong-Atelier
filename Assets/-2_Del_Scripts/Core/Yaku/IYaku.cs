namespace MahjongAtelier.Core
{
    /// <summary>
    /// 모든 역(役)이 구현하는 인터페이스.
    /// 
    /// 사용 패턴:
    ///   var yaku = new Tanyao();
    ///   if (yaku.IsApplicable(context, decomposition))
    ///       int han = yaku.GetHan(context, decomposition);
    /// 
    /// 각 역은 stateless로 만들어 싱글톤처럼 재사용 가능.
    /// </summary>
    public interface IYaku
    {
        /// <summary>역 이름 (한자 또는 일본어 표기). UI 표시용.</summary>
        string Name { get; }

        /// <summary>역 이름 (한글). 한국어 UI용.</summary>
        string NameKr { get; }

        /// <summary>역만 여부. 역만이면 GetHan 대신 GetYakuman을 사용.</summary>
        bool IsYakuman { get; }

        /// <summary>
        /// 이 역이 성립하는지 판정.
        /// </summary>
        bool IsApplicable(YakuContext ctx, HandDecomposition decomp);

        /// <summary>
        /// 멘젠 시 판수. IsApplicable이 true일 때만 의미 있음.
        /// </summary>
        int GetHan(YakuContext ctx, HandDecomposition decomp);

        /// <summary>
        /// 후로 시 판수. 0이면 후로하면 사라지는 역 (예: 리치는 후로 불가).
        /// 멘젠 한정 역은 IsApplicable에서 멘젠 체크해서 false 반환하는 게 일반적.
        /// </summary>
        int GetHanCalled(YakuContext ctx, HandDecomposition decomp);
    }
}
