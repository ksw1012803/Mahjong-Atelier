using System.Collections.Generic;
using UnityEngine;

namespace MahjongAtelier.Core
{
    /// <summary>
    /// 론 분석 v2 — 후로 지원 + 디버그 로그 + RonCandidate 포함.
    /// </summary>
    public static class RonAnalyzer
    {
        /// <summary>디버그 로그 활성화. 문제 진단 시 true로.</summary>
        public static bool DebugLog = true;

        public static List<RonCandidate> FindCandidates(
            int discarderIndex,
            TileData discardedTile,
            IReadOnlyList<MahjongPlayer> players,
            YakuChecker yakuChecker,
            System.Func<MahjongPlayer, TileKind, bool, YakuContext> contextBuilder)
        {
            var candidates = new List<RonCandidate>();
            if (players == null || discardedTile == null) return candidates;

            if (DebugLog) Debug.Log($"[RonAnalyzer] 검사 시작. 버린 패: {discardedTile}, 버린 사람: P{discarderIndex}");

            for (int i = 0; i < players.Count; i++)
            {
                if (i == discarderIndex) continue;
                var player = players[i];

                // 손패 수 검증: 후로 면자 * 3을 뺀 만큼이어야 13장 = 텐파이 가능
                int expectedHandSize = 13 - (player.CalledMeldsExt.Count * 3);
                if (player.HandTiles.Count != expectedHandSize)
                {
                    if (DebugLog) Debug.Log($"[RonAnalyzer] P{i}: 손패 수 불일치 ({player.HandTiles.Count}/{expectedHandSize}) → 스킵");
                    continue;
                }

                var candidate = EvaluatePlayer(player, discardedTile, yakuChecker, contextBuilder, i);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                    if (DebugLog) Debug.Log($"[RonAnalyzer] P{i}: 론 가능!");
                }
            }

            if (DebugLog) Debug.Log($"[RonAnalyzer] 검사 완료. 후보 {candidates.Count}명");
            return candidates;
        }

        private static RonCandidate EvaluatePlayer(
            MahjongPlayer player,
            TileData discardedTile,
            YakuChecker yakuChecker,
            System.Func<MahjongPlayer, TileKind, bool, YakuContext> contextBuilder,
            int playerIdx)
        {
            // 1) 가상으로 패 추가 → 화료 형태 체크
            player.HandTiles.Add(discardedTile);
            bool isAgariForm = player.IsAgari();
            bool isValidWin = player.IsValidAgariTile(discardedTile.Kind);
            player.HandTiles.Remove(discardedTile);

            if (!isAgariForm)
            {
                if (DebugLog) Debug.Log($"[RonAnalyzer] P{playerIdx}: 화료 형태 아님");
                return null;
            }
            if (!isValidWin)
            {
                if (DebugLog) Debug.Log($"[RonAnalyzer] P{playerIdx}: 리치 후 비대기패");
                return null;
            }

            // 2) 대기패 검사 (13장 시점)
            var waits = player.GetWaitingTiles();
            if (!waits.Contains(discardedTile.Kind))
            {
                if (DebugLog)
                {
                    var waitsStr = string.Join(",", waits);
                    Debug.Log($"[RonAnalyzer] P{playerIdx}: 대기패 아님. 대기: [{waitsStr}], 버려진: {discardedTile.Kind}");
                }
                return null;
            }

            // 3) 후리텐 검사
            if (FuritenAnalyzer.IsFuriten(player, waits))
            {
                if (DebugLog) Debug.Log($"[RonAnalyzer] P{playerIdx}: 후리텐");
                return null;
            }

            // 4) 역 검사 — 14장 가상 손패로
            player.HandTiles.Add(discardedTile);
            var ctx = contextBuilder(player, discardedTile.Kind, false);
            var yakuResult = yakuChecker.Check(ctx);
            player.HandTiles.Remove(discardedTile);

            if (!yakuResult.HasAnyYaku)
            {
                if (DebugLog) Debug.Log($"[RonAnalyzer] P{playerIdx}: 역 없음");
                return null;
            }

            return new RonCandidate
            {
                PlayerIndex = player.Index,
                YakuResult = yakuResult
            };
        }
    }

    /// <summary>론 가능한 플레이어 한 명의 정보.</summary>
    public sealed class RonCandidate
    {
        public int PlayerIndex { get; set; }
        public YakuResult YakuResult { get; set; }
    }
}