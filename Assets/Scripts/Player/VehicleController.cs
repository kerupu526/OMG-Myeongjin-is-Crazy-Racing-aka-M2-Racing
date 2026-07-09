using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace M2.Player
{
    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Speed")]
        public float maxSpeed = 20f;
        public float acceleration = 12f;
        public float deceleration = 8f;
        public float reverseSpeed = 8f;
        [Tooltip("Passive coast-down when no throttle/reverse input is held.")]
        public float passiveDeceleration = 6f;
        [Tooltip("Deceleration applied while the brake key (Shift) is held.")]
        public float brakeDeceleration = 25f;

        [Header("Steering")]
        public float turnSpeed = 140f;
        [Tooltip("0-1 factor applied to turnSpeed when at maxSpeed. Lower = harder to turn at high speed.")]
        public float turnSpeedAtMaxVelocity = 0.5f;
        [Tooltip("Steering only takes effect once |currentSpeed| exceeds this — no turning in place while stopped.")]
        public float minSpeedToSteer = 0.5f;

        public event Action OnAccelItemUsed;
        public event Action OnAttackDefenseItemUsed;
        // Fires when a hit actually lands (not on a blocked/shielded hit) — stage systems
        // hook into this for stage-specific hit consequences (e.g. 비키니시티 "비법" drop).
        public event Action OnHitByAttackItem;

        // --- Debug/test readouts ---
        public float CurrentSpeed => currentSpeed;
        public float CurrentAcceleration { get; private set; }
        // Time-window based rather than an Enter/Exit counter: physics contacts can flicker
        // apart and back together within the same collision, which desynced a counter.
        public bool IsColliding => Time.time - lastCollisionTime < collisionMemory;

        Rigidbody rb;
        InputAction steerAction;
        InputAction throttleAction;
        InputAction brakeAction;
        InputAction useAccelItemAction;
        InputAction useAttackDefenseItemAction;

        float currentSpeed;
        float itemSpeedBonus;
        bool isStunned;
        bool inputLocked;
        bool steeringInverted;
        bool isKnockedBack;
        bool hasShield;
        Coroutine speedBoostRoutine;
        Coroutine stunRoutine;
        Coroutine steeringInvertRoutine;
        Coroutine knockbackRoutine;
        Coroutine shieldRoutine;
        float lastCollisionTime = -10f;
        const float collisionMemory = 0.15f;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            steerAction = new InputAction("Steer", InputActionType.Value, expectedControlType: "Axis");
            steerAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/leftArrow")
                .With("Negative", "<Keyboard>/a")
                .With("Positive", "<Keyboard>/rightArrow")
                .With("Positive", "<Keyboard>/d");

            throttleAction = new InputAction("Throttle", InputActionType.Value, expectedControlType: "Axis");
            throttleAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/downArrow")
                .With("Negative", "<Keyboard>/s")
                .With("Positive", "<Keyboard>/upArrow")
                .With("Positive", "<Keyboard>/w");

            brakeAction = new InputAction("Brake", InputActionType.Button, "<Keyboard>/leftShift");

            useAccelItemAction = new InputAction("UseAccelItem", InputActionType.Button, "<Keyboard>/leftCtrl");
            useAttackDefenseItemAction = new InputAction("UseAttackDefenseItem", InputActionType.Button, "<Keyboard>/e");
        }

        void OnEnable()
        {
            steerAction.Enable();
            throttleAction.Enable();
            brakeAction.Enable();
            useAccelItemAction.Enable();
            useAttackDefenseItemAction.Enable();

            useAccelItemAction.performed += HandleAccelItemUsed;
            useAttackDefenseItemAction.performed += HandleAttackDefenseItemUsed;
        }

        void OnDisable()
        {
            useAccelItemAction.performed -= HandleAccelItemUsed;
            useAttackDefenseItemAction.performed -= HandleAttackDefenseItemUsed;

            steerAction.Disable();
            throttleAction.Disable();
            brakeAction.Disable();
            useAccelItemAction.Disable();
            useAttackDefenseItemAction.Disable();
        }

        void HandleAccelItemUsed(InputAction.CallbackContext ctx) => OnAccelItemUsed?.Invoke();
        void HandleAttackDefenseItemUsed(InputAction.CallbackContext ctx) => OnAttackDefenseItemUsed?.Invoke();

        void FixedUpdate()
        {
            float speedBeforeUpdate = currentSpeed;

            // Facing is otherwise controlled entirely by MoveRotation in ApplySteering, but a
            // hard collision can still hand the Rigidbody leftover angular velocity from the
            // physics solver. Left unchecked, that spins the car slightly after every crash —
            // transform.forward drifts away from where the player expects it, so reversing
            // away from a wall can look/feel like it's not responding at all.
            rb.angularVelocity = Vector3.zero;

            if (isStunned || inputLocked)
            {
                currentSpeed = 0f;
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
            else if (isKnockedBack)
            {
                // Leave rb.linearVelocity alone so the knockback impulse (and physics drag)
                // actually carries the car instead of being overwritten by throttle control.
            }
            else
            {
                float throttleInput = throttleAction.ReadValue<float>();
                float steerInput = steerAction.ReadValue<float>();
                bool isBraking = brakeAction.IsPressed();

                ApplyThrottle(throttleInput, isBraking);
                ApplySteering(steeringInverted ? -steerInput : steerInput);
            }

            CurrentAcceleration = (currentSpeed - speedBeforeUpdate) / Time.fixedDeltaTime;
        }

        void OnCollisionEnter(Collision collision) => lastCollisionTime = Time.time;
        void OnCollisionStay(Collision collision) => lastCollisionTime = Time.time;

        void ApplyThrottle(float throttleInput, bool isBraking)
        {
            float effectiveMaxSpeed = maxSpeed + itemSpeedBonus;

            if (isBraking)
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeDeceleration * Time.fixedDeltaTime);
            }
            else if (Mathf.Approximately(throttleInput, 0f))
            {
                currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, passiveDeceleration * Time.fixedDeltaTime);
            }
            else if (throttleInput > 0f)
            {
                float target = throttleInput * effectiveMaxSpeed;
                float rate = currentSpeed < target ? acceleration : deceleration;
                currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.fixedDeltaTime);
            }
            else
            {
                // Down/S: reverse, not brake. If still moving forward, kill that momentum
                // at the brake rate first so reverse feels immediate rather than crawling
                // down at the normal accel rate; once past zero, ramp into reverse normally.
                float target = throttleInput * reverseSpeed;
                float rate = currentSpeed > 0f ? brakeDeceleration : acceleration;
                currentSpeed = Mathf.MoveTowards(currentSpeed, target, rate * Time.fixedDeltaTime);
            }

            Vector3 forward = transform.forward;
            forward.y = 0f;
            Vector3 velocity = forward.normalized * currentSpeed;
            velocity.y = rb.linearVelocity.y;
            rb.linearVelocity = velocity;
        }

        void ApplySteering(float steerInput)
        {
            if (Mathf.Abs(currentSpeed) < minSpeedToSteer) return;

            float speedFactor = Mathf.Abs(currentSpeed) / Mathf.Max(maxSpeed, 0.0001f);
            float turnFactor = Mathf.Lerp(1f, turnSpeedAtMaxVelocity, speedFactor);

            float speedSign = currentSpeed < 0f ? -1f : 1f;
            float yaw = steerInput * turnSpeed * turnFactor * speedSign * Time.fixedDeltaTime;
            Quaternion deltaRotation = Quaternion.Euler(0f, yaw, 0f);
            rb.MoveRotation(rb.rotation * deltaRotation);
        }

        // --- Input lock (briefing / countdown / race-end) ---

        public void SetInputLocked(bool locked)
        {
            inputLocked = locked;
            if (locked)
            {
                currentSpeed = 0f;
                rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            }
        }

        // --- Steering invert (아프리카TV 방송사고 존) ---

        public void SetSteeringInverted(bool inverted)
        {
            steeringInverted = inverted;
        }

        public void SetSteeringInvertedFor(float duration)
        {
            if (steeringInvertRoutine != null) StopCoroutine(steeringInvertRoutine);
            steeringInvertRoutine = StartCoroutine(SteeringInvertRoutine(duration));
        }

        IEnumerator SteeringInvertRoutine(float duration)
        {
            steeringInverted = true;
            yield return new WaitForSeconds(duration);
            steeringInverted = false;
            steeringInvertRoutine = null;
        }

        // --- Knockback (네더요새 가스트 파이어볼) ---

        // Sets velocity directly to the impulse for `duration` and suspends normal throttle
        // control so the knockback (and physics drag) actually carries the car off course
        // instead of being overwritten by ApplyThrottle on the very next FixedUpdate.
        public void ApplyKnockback(Vector3 impulse, float duration = 0.8f)
        {
            if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
            currentSpeed = 0f;
            rb.linearVelocity = new Vector3(impulse.x, rb.linearVelocity.y, impulse.z);
            knockbackRoutine = StartCoroutine(KnockbackRoutine(duration));
        }

        IEnumerator KnockbackRoutine(float duration)
        {
            isKnockedBack = true;
            yield return new WaitForSeconds(duration);
            isKnockedBack = false;
            knockbackRoutine = null;
        }

        // --- Item effect hooks (M2.Items acts on the vehicle through these) ---

        public void ApplySpeedBoost(float bonusSpeed, float duration)
        {
            if (speedBoostRoutine != null) StopCoroutine(speedBoostRoutine);
            speedBoostRoutine = StartCoroutine(SpeedBoostRoutine(bonusSpeed, duration));
        }

        IEnumerator SpeedBoostRoutine(float bonusSpeed, float duration)
        {
            itemSpeedBonus = bonusSpeed;
            yield return new WaitForSeconds(duration);
            itemSpeedBonus = 0f;
            speedBoostRoutine = null;
        }

        // CLAUDE.md: on being hit, stop immediately then gradually reaccelerate.
        // The gradual part falls out for free — once isStunned clears, ApplyThrottle
        // ramps currentSpeed back up at the normal `acceleration` rate rather than snapping.
        public void ApplyHitStun(float stunDuration = 0.6f)
        {
            if (stunRoutine != null) StopCoroutine(stunRoutine);
            stunRoutine = StartCoroutine(HitStunRoutine(stunDuration));
            OnHitByAttackItem?.Invoke();
        }

        IEnumerator HitStunRoutine(float stunDuration)
        {
            isStunned = true;
            yield return new WaitForSeconds(stunDuration);
            isStunned = false;
            stunRoutine = null;
        }

        public void ActivateShield(float duration)
        {
            if (shieldRoutine != null) StopCoroutine(shieldRoutine);
            hasShield = true;
            shieldRoutine = StartCoroutine(ShieldRoutine(duration));
        }

        IEnumerator ShieldRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            hasShield = false;
            shieldRoutine = null;
        }

        // One-time-use: a shield consumes itself the moment it blocks a hit,
        // even if its timed duration hasn't run out yet.
        public bool TryConsumeShield()
        {
            if (!hasShield) return false;

            hasShield = false;
            if (shieldRoutine != null)
            {
                StopCoroutine(shieldRoutine);
                shieldRoutine = null;
            }
            return true;
        }
    }
}
