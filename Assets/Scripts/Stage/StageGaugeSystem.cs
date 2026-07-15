using System;
using M2.Core;
using M2.Network;
using UnityEngine;

namespace M2.Stage
{
    // Shared gauge logic for all 3 stages (비키니시티/아프리카TV/네더요새).
    // A stage-specific gauge subclasses this and only needs to set maxValue/passiveRatePerSecond
    // and implement what happens when the gauge bottoms out or recovers from that state.
    // CLAUDE.md: "게이지 로직은 상속으로 재사용, 중복 구현 금지."
    public abstract class StageGaugeSystem : MonoBehaviour
    {
        [Header("Gauge")]
        public float maxValue = 100f;
        [Tooltip("Passive change per second while running. Negative drains the gauge (e.g. oxygen), positive fills it (e.g. mental/temperature).")]
        public float passiveRatePerSecond = -2f;
        [Tooltip("false(기본): 0에 도달하면 위험 상태(산소 고갈 등). true: maxValue에 도달하면 위험 상태(멘탈/체온 만땅). 시작값도 이 방향에 맞춰 안전한 쪽(false=가득 참, true=0)으로 자동 설정됨.")]
        public bool dangerAtMax = false;

        public float CurrentValue { get; private set; }
        public bool IsDepleted { get; private set; }

        public event Action<float, float> OnValueChanged; // (current, max)
        public event Action OnDepleted;
        public event Action OnRecovered;

        GameManager gameManager;
        NetworkRaceManager networkRaceManager;

        protected virtual void Awake()
        {
            CurrentValue = dangerAtMax ? 0f : maxValue;
            // Passive tick must not run before the race actually starts — without this gate,
            // a dangerAtMax gauge (temperature/mental) with no grace period on depletion (e.g.
            // NetherFortress's instant burn game-over) could hit max while the player is still
            // reading the Briefing screen or waiting through Countdown, locking them out before
            // they ever get to drive. GameManager may not exist yet in edit-mode contexts
            // (PlayMode tests building a bare gauge in isolation), hence the null check below.
            gameManager = FindFirstObjectByType<GameManager>();
        }

        protected virtual void Update()
        {
            // NetworkRace deliberately leaves each client's local GameManager idle; use the
            // replicated manager state there so a locally owned gauge starts and stops on the
            // same countdown/race boundaries as the host.
            if (networkRaceManager == null)
            {
                networkRaceManager = FindFirstObjectByType<NetworkRaceManager>();
            }

            if (networkRaceManager != null && networkRaceManager.IsSpawned)
            {
                if (networkRaceManager.State != RaceState.Racing) return;
            }
            else if (gameManager != null && gameManager.CurrentState != RaceState.Racing)
            {
                return;
            }

            ModifyValue(passiveRatePerSecond * Time.deltaTime);
        }

        /// <summary>
        /// Restores this gauge to its safe starting value for a new round. Network races retain
        /// their player objects between rematches, so simply resetting the vehicle is not enough.
        /// </summary>
        public void ResetGauge()
        {
            bool wasDepleted = IsDepleted;
            CurrentValue = dangerAtMax ? 0f : maxValue;
            IsDepleted = false;

            if (wasDepleted)
            {
                OnRecovered?.Invoke();
                HandleRecovered();
            }

            OnValueChanged?.Invoke(CurrentValue, maxValue);
        }

        // Positive delta fills the gauge, negative drains it. Used both by the passive
        // per-frame tick and by one-off effects (item pickups, hits, stage hazards).
        public void ModifyValue(float delta)
        {
            float previous = CurrentValue;
            CurrentValue = Mathf.Clamp(CurrentValue + delta, 0f, maxValue);
            if (Mathf.Approximately(CurrentValue, previous)) return;

            OnValueChanged?.Invoke(CurrentValue, maxValue);

            bool wasDepleted = IsInDangerZone(previous);
            bool isDepletedNow = IsInDangerZone(CurrentValue);

            if (isDepletedNow && !wasDepleted)
            {
                IsDepleted = true;
                OnDepleted?.Invoke();
                HandleDepleted();
            }
            else if (!isDepletedNow && wasDepleted)
            {
                IsDepleted = false;
                OnRecovered?.Invoke();
                HandleRecovered();
            }
        }

        bool IsInDangerZone(float value) => dangerAtMax ? value >= maxValue : value <= 0f;

        // Stage-specific reaction to the gauge bottoming out (game over, lockout, etc.)
        protected abstract void HandleDepleted();

        // Stage-specific reaction to recovering out of the depleted state before
        // any depletion consequence (e.g. a game-over timer) finishes.
        protected abstract void HandleRecovered();
    }
}
