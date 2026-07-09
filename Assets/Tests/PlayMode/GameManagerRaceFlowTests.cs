using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using M2.Core;
using M2.Player;

namespace M2.Tests.PlayMode
{
    public class GameManagerRaceFlowTests
    {
        GameObject vehicleObject;
        VehicleController vehicle;
        LapTracker lapTracker;
        GameObject timerObject;
        RaceTimer raceTimer;
        GameObject gmObject;
        GameManager gm;

        [SetUp]
        public void SetUp()
        {
            // Two checkpoints (0 = finish, 1) — a single-checkpoint track never completes a
            // lap because Checkpoint.index 0 doubles as both "expected first pass" and
            // "finish line" in LapTracker's state machine; see LapTrackerTests for the same setup.
            CreateCheckpoint(0);
            CreateCheckpoint(1);

            vehicleObject = new GameObject("TestVehicle");
            vehicleObject.AddComponent<Rigidbody>();
            vehicle = vehicleObject.AddComponent<VehicleController>();
            lapTracker = vehicleObject.AddComponent<LapTracker>();

            timerObject = new GameObject("TestRaceTimer");
            raceTimer = timerObject.AddComponent<RaceTimer>();
            raceTimer.lapTracker = lapTracker;

            gmObject = new GameObject("TestGameManager");
            gm = gmObject.AddComponent<GameManager>();
            gm.racers.Add(lapTracker);
            gm.vehicles.Add(vehicle);
            gm.raceTimer = raceTimer;
            gm.briefingDuration = 0.05f;
            gm.countdownSeconds = 1;
            gm.targetLapCount = 1;
            gm.lap1TimeLimit = 3f;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var cp in Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None))
            {
                Object.Destroy(cp.gameObject);
            }
            Object.Destroy(vehicleObject);
            Object.Destroy(timerObject);
            Object.Destroy(gmObject);
        }

        static void CreateCheckpoint(int index)
        {
            var go = new GameObject($"Checkpoint_{index}");
            go.AddComponent<BoxCollider>().isTrigger = true;
            var cp = go.AddComponent<Checkpoint>();
            cp.index = index;
        }

        [UnityTest]
        public IEnumerator Race_Reaches_Racing_State_After_Briefing_And_Countdown()
        {
            yield return WaitForState(RaceState.Racing);

            Assert.AreEqual(RaceState.Racing, gm.CurrentState, "GameManager should reach Racing state after briefing+countdown.");
        }

        [UnityTest]
        public IEnumerator Completing_Target_Laps_Wins_The_Race()
        {
            LapTracker winner = null;
            gm.OnRaceWon += w => winner = w;

            yield return WaitForState(RaceState.Racing);

            lapTracker.NotifyCheckpointPassed(1);
            lapTracker.NotifyCheckpointPassed(0);

            yield return null; // let the event propagate

            Assert.AreEqual(RaceState.Finished, gm.CurrentState);
            Assert.AreSame(lapTracker, winner, "The racer that completed the target lap count should be reported as the winner.");
        }

        [UnityTest]
        public IEnumerator Draw_When_Time_Limit_Expires_With_No_Finisher()
        {
            gm.lap1TimeLimit = 0.2f; // short so the test doesn't wait long
            bool drawFired = false;
            gm.OnRaceDraw += () => drawFired = true;

            yield return WaitForState(RaceState.Racing);
            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(drawFired, "Time limit expiring with nobody finishing should raise OnRaceDraw.");
            Assert.AreEqual(RaceState.Finished, gm.CurrentState);
        }

        IEnumerator WaitForState(RaceState state)
        {
            float timeout = Time.time + 3f;
            while (gm.CurrentState != state && Time.time < timeout)
            {
                yield return null;
            }
        }
    }
}
