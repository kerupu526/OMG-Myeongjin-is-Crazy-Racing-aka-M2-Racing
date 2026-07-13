using M2.Core;
using NUnit.Framework;
using UnityEngine;

namespace M2.Tests.PlayMode
{
    public class RaceResultResolverTests
    {
        GameObject firstObject;
        GameObject secondObject;
        LapTracker first;
        LapTracker second;

        [SetUp]
        public void SetUp()
        {
            firstObject = new GameObject("FirstRacer");
            secondObject = new GameObject("SecondRacer");
            first = firstObject.AddComponent<LapTracker>();
            second = secondObject.AddComponent<LapTracker>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(firstObject);
            Object.DestroyImmediate(secondObject);
        }

        [Test]
        public void StarBet_Prefers_Stars_Before_FinishTime()
        {
            RaceFinishResult[] results =
            {
                new RaceFinishResult { racer = first, finished = true, stars = 4, finishTime = 100f },
                new RaceFinishResult { racer = second, finished = true, stars = 5, finishTime = 130f },
            };

            Assert.AreSame(second, RaceResultResolver.ResolveStarBet(results, out string reason));
            Assert.IsNull(reason);
        }

        [Test]
        public void StarBet_Uses_Faster_Time_As_Tiebreaker()
        {
            RaceFinishResult[] results =
            {
                new RaceFinishResult { racer = first, finished = true, stars = 5, finishTime = 111f },
                new RaceFinishResult { racer = second, finished = true, stars = 5, finishTime = 110f },
            };

            Assert.AreSame(second, RaceResultResolver.ResolveStarBet(results, out _));
        }

        [Test]
        public void StarBet_Draws_When_Nobody_Finishes()
        {
            RaceFinishResult[] results =
            {
                new RaceFinishResult { racer = first, finished = false },
                new RaceFinishResult { racer = second, finished = false },
            };

            Assert.IsNull(RaceResultResolver.ResolveStarBet(results, out string reason));
            Assert.AreEqual("제한시간 초과", reason);
        }

        [Test]
        public void StarBet_Draws_On_Exact_Star_And_Time_Tie()
        {
            RaceFinishResult[] results =
            {
                new RaceFinishResult { racer = first, finished = true, stars = 6, finishTime = 100f },
                new RaceFinishResult { racer = second, finished = true, stars = 6, finishTime = 100f },
            };

            Assert.IsNull(RaceResultResolver.ResolveStarBet(results, out string reason));
            Assert.AreEqual("별점 및 완주시간 동점", reason);
        }
    }
}
