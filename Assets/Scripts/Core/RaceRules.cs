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

                if (!best.HasValue || candidate.stars > best.Value.stars ||
                    (candidate.stars == best.Value.stars && candidate.finishTime < best.Value.finishTime - 0.001f))
                {
                    best = candidate;
                    tied = false;
                }
                else if (candidate.stars == best.Value.stars &&
                    System.Math.Abs(candidate.finishTime - best.Value.finishTime) <= 0.001f)
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
