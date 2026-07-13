using System.Collections.Generic;

namespace M2.Core
{
    public enum VictoryCondition
    {
        SimpleFinish,
        StarBet,
    }

    public enum RaceMode
    {
        Item,
        Speed,
    }

    public static class RaceModeRules
    {
        public const int SpeedModeLapCount = 5;
        public const float SpeedModeMaximumKph = 100f;
        public const float SpeedModeGasolineInterval = 5f;
        public const float KphToMetersPerSecond = 1f / 3.6f;

        public static int NormalizeItemLapCount(int requestedLapCount)
        {
            if (requestedLapCount <= 1) return 1;
            if (requestedLapCount <= 3) return 3;
            return 5;
        }
    }

    public interface IRaceStarProvider
    {
        int ComputeTotalStars(float finishTimeSeconds);
    }

    public struct RaceFinishResult
    {
        public LapTracker racer;
        public bool finished;
        public int stars;
        public float finishTime;
    }

    /// <summary>
    /// Shared placement ordering for the local and online result views. A completed race always
    /// ranks ahead of an unfinished one; star-bet races then prefer stars before finish time.
    /// A zero result means a genuine tie and receives the same displayed rank.
    /// </summary>
    public static class RaceResultOrdering
    {
        const float TimeTieTolerance = 0.001f;

        public static int Compare(RaceFinishResult left, RaceFinishResult right,
            VictoryCondition victoryCondition)
        {
            if (left.finished != right.finished) return left.finished ? -1 : 1;
            if (!left.finished) return 0;

            if (victoryCondition == VictoryCondition.StarBet && left.stars != right.stars)
                return right.stars.CompareTo(left.stars);

            float difference = left.finishTime - right.finishTime;
            if (System.Math.Abs(difference) <= TimeTieTolerance) return 0;
            return difference < 0f ? -1 : 1;
        }

        public static void GetPairRanks(RaceFinishResult first, RaceFinishResult second,
            VictoryCondition victoryCondition, out int firstRank, out int secondRank)
        {
            int comparison = Compare(first, second, victoryCondition);
            if (comparison == 0)
            {
                firstRank = 1;
                secondRank = 1;
            }
            else if (comparison < 0)
            {
                firstRank = 1;
                secondRank = 2;
            }
            else
            {
                firstRank = 2;
                secondRank = 1;
            }
        }
    }

    public static class RaceResultResolver
    {
        public static LapTracker ResolveStarBet(IReadOnlyList<RaceFinishResult> results,
            out string drawReason)
        {
            drawReason = "제한시간 초과";
            RaceFinishResult? best = null;
            bool tied = false;

            for (int i = 0; i < results.Count; i++)
            {
                RaceFinishResult candidate = results[i];
                if (!candidate.finished) continue;

                if (!best.HasValue ||
                    RaceResultOrdering.Compare(candidate, best.Value, VictoryCondition.StarBet) < 0)
                {
                    best = candidate;
                    tied = false;
                }
                else if (RaceResultOrdering.Compare(candidate, best.Value, VictoryCondition.StarBet) == 0)
                {
                    tied = true;
                }
            }

            if (!best.HasValue) return null;
            if (tied)
            {
                drawReason = "별점 및 완주시간 동점";
                return null;
            }

            drawReason = null;
            return best.Value.racer;
        }
    }
}
