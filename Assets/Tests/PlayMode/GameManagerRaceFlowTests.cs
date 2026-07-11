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
            string reason = null;
            gm.OnRaceDraw += r => { drawFired = true; reason = r; };

            yield return WaitForState(RaceState.Racing);
            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(drawFired, "Time limit expiring with nobody finishing should raise OnRaceDraw.");
            Assert.AreEqual("제한시간 초과", reason, "A timeout draw should report the timeout reason.");
            Assert.AreEqual(RaceState.Finished, gm.CurrentState);
        }

        [UnityTest]
        public IEnumerator EndRaceAsDraw_Lets_A_Stage_Hazard_End_The_Race_Early_With_A_Custom_Reason()
        {
            // Regression coverage for Nether Fortress's burn game over — before this, reaching
            // max temperature only showed a small stage-specific overlay and GameManager never
            // learned the race was over, so the real result screen never appeared and the race
            // timer kept running underneath. Playtester ask: "패배한 걸로 치고 일단 무승부
            // 처리를 내자".
            string reason = null;
            gm.OnRaceDraw += r => reason = r;

            yield return WaitForState(RaceState.Racing);

            gm.EndRaceAsDraw("화상");

            Assert.AreEqual(RaceState.Finished, gm.CurrentState);
            Assert.AreEqual("화상", reason, "EndRaceAsDraw should report the caller's custom reason, not the timeout default.");
            Assert.AreEqual(0f, vehicle.CurrentSpeed, 0.01f, "Ending the race should lock vehicle input like any other race end.");
        }

        [UnityTest]
        public IEnumerator Manual_Start_Waits_For_RequestStart_Instead_Of_A_Timer()
        {
            gm.waitForManualStart = true;
            gm.briefingDuration = 0f; // irrelevant when waitForManualStart is true — prove it's ignored

            yield return WaitForState(RaceState.Briefing);

            // Give it several frames — with a timer this would already have moved on since
            // briefingDuration is 0, but manual-start mode should still be waiting.
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(RaceState.Briefing, gm.CurrentState, "Should stay in Briefing until RequestStart() is called.");

            gm.RequestStart();
            yield return WaitForState(RaceState.Racing);

            Assert.AreEqual(RaceState.Racing, gm.CurrentState, "RequestStart() should let the flow proceed to Countdown then Racing.");
        }

        [UnityTest]
        public IEnumerator AutoStartDisabled_Waits_For_BeginRaceFlow()
        {
            // The networked scene (NetworkRaceManager) sets autoStartOnStart=false and starts the
            // flow itself once both players spawn, registering their racers via RegisterRacer.
            // Replace SetUp's auto-starting GameManager with one in that networked configuration.
            // DestroyImmediate before yielding so SetUp's gm never runs its (auto-started) flow.
            Object.DestroyImmediate(gmObject);

            gmObject = new GameObject("NetworkedGameManager");
            gm = gmObject.AddComponent<GameManager>();
            gm.autoStartOnStart = false; // set before Start() runs (Start fires next frame)
            gm.raceTimer = raceTimer;
            gm.briefingDuration = 0.05f;
            gm.countdownSeconds = 1;
            gm.targetLapCount = 1;

            // No racers registered yet, autoStart off — must stay in PreRace across several frames.
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(RaceState.PreRace, gm.CurrentState,
                "With autoStartOnStart=false and no BeginRaceFlow() call, the race must not start on its own.");

            // Register the racer the way the host's NetworkRaceManager does, then start the flow.
            gm.RegisterRacer(lapTracker, vehicle);
            Assert.Contains(lapTracker, gm.racers, "RegisterRacer should add the racer to the GameManager's list.");

            gm.BeginRaceFlow();
            yield return WaitForState(RaceState.Racing);
            Assert.AreEqual(RaceState.Racing, gm.CurrentState,
                "BeginRaceFlow() should drive the flow through briefing/countdown into Racing.");

            // Idempotent: a second BeginRaceFlow() must not restart or double-run the flow.
            gm.BeginRaceFlow();
            Assert.AreEqual(RaceState.Racing, gm.CurrentState, "A repeated BeginRaceFlow() should be a no-op.");
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
