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
        [Tooltip("false(기본): 0에 도달하면 위험 상태(산소 고갈 등). true: maxValue에 도달하면 위험 상태(멘탈/체온 만땅). 시작값도 이 방향에 맞춰 안전한 쪽(false=가득 참, true=0)으로 자동 설정됨.")]
        public bool dangerAtMax = false;

        public float CurrentValue { get; private set; }
        public bool IsDepleted { get; private set; }

        public event Action<float, float> OnValueChanged; // (current, max)
        public event Action OnDepleted;
        public event Action OnRecovered;

        protected virtual void Awake()
        {
            CurrentValue = dangerAtMax ? 0f : maxValue;
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
