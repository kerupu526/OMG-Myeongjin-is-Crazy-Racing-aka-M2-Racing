using System;
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

        public float CurrentValue { get; private set; }
        public bool IsDepleted { get; private set; }

        public event Action<float, float> OnValueChanged; // (current, max)
        public event Action OnDepleted;
        public event Action OnRecovered;

        protected virtual void Awake()
        {
            CurrentValue = maxValue;
        }

        protected virtual void Update()
        {
            ModifyValue(passiveRatePerSecond * Time.deltaTime);
        }

        // Positive delta fills the gauge, negative drains it. Used both by the passive
        // per-frame tick and by one-off effects (item pickups, hits, stage hazards).
        public void ModifyValue(float delta)
        {
            float previous = CurrentValue;
            CurrentValue = Mathf.Clamp(CurrentValue + delta, 0f, maxValue);
            if (Mathf.Approximately(CurrentValue, previous)) return;

            OnValueChanged?.Invoke(CurrentValue, maxValue);

            bool wasDepleted = previous <= 0f;
            bool isDepletedNow = CurrentValue <= 0f;

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

        // Stage-specific reaction to the gauge bottoming out (game over, lockout, etc.)
        protected abstract void HandleDepleted();

        // Stage-specific reaction to recovering out of the depleted state before
        // any depletion consequence (e.g. a game-over timer) finishes.
        protected abstract void HandleRecovered();
    }
}
