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
        [Tooltip("Deceleration used to kill forward momentum quickly when the player presses reverse (Down/S) while still moving forward.")]
        public float brakeDeceleration = 25f;
        [Tooltip("Extra currentSpeed bled off per second while actively scraping a wall (not just resting against it) — without this, the wall-slide fix removes the into-wall velocity component but leaves speed untouched, so grinding along a wall costs nothing and feels frictionless/on-rails.")]
        public float wallScrapeDeceleration = 10f;
        [Tooltip("One-shot outward bounce applied on wall impact (same cooldown/timing as OnWallHit, and same 'bounces but never loses control' mechanism as ApplyBounce/bounceDecay used for the ghast fireball) — gives scraping a wall a physical bump instead of just slowing down.")]
        public float wallBounceStrength = 3f;

        [Header("Steering")]
        public float turnSpeed = 140f;
        [Tooltip("0-1 factor applied to turnSpeed when at maxSpeed. Lower = harder to turn at high speed.")]
        public float turnSpeedAtMaxVelocity = 0.5f;
        [Tooltip("Steering only takes effect once |currentSpeed| exceeds this — no turning in place while stopped.")]
        public float minSpeedToSteer = 0.5f;

        [Tooltip("How fast a bounce shove (e.g. ghast fireball) bleeds back to zero, in m/s per second.")]
        public float bounceDecay = 25f;

        [Tooltip("Minimum time between OnWallHit events while continuously scraping a wall, so one multi-second slide doesn't fire dozens of hits in a row.")]
        public float wallHitEventCooldown = 1f;

        [Header("Drift (Shift)")]
        [Tooltip("Hold Shift while steering to drift — the car's facing turns faster than its actual travel direction (moveDirection lags behind via driftSlipRecoverySpeed), so it slides through the corner instead of gripping. Release Shift for a short speed boost (마리오카트식 미니터보), scaled by how long you held the drift.")]
        public float driftTurnMultiplier = 1.6f;
        [Tooltip("How fast the car's actual travel direction catches up to its facing while drifting, in degrees/sec. Lower = more slide.")]
        public float driftSlipRecoverySpeed = 200f;
        [Tooltip("Minimum hold time before releasing grants any boost — stops accidental taps from giving a free boost.")]
        public float minDriftHoldTimeForBoost = 0.3f;
        // Was 1.5s — playtester feedback: "홀드 타이밍이 조금 어색함", specifically that most
        // corners aren't long enough to hold a drift for 1.5s, so full charge almost never
        // happened in practice. 0.9s fits a typical corner's drift window much better while
        // still rewarding a longer, more committed slide over a quick tap-drift.
        [Tooltip("Hold time (seconds) to reach the maximum boost bonus — charge scales linearly up to this.")]
        public float driftBoostChargeTime = 0.9f;
        // Was 8 — playtester feedback: "부스트 속도가 좀 약한 것 같음". 12 is +50%, closer to a
        // third of maxSpeed's default 20 rather than a fifth, more of a genuine mini-turbo.
        [Tooltip("Speed bonus granted at max charge (same units as maxSpeed).")]
        public float maxDriftBoostSpeed = 12f;
        [Tooltip("How long the release boost lasts.")]
        public float driftBoostDuration = 1f;

        public event Action OnAccelItemUsed;
        public event Action OnAttackDefenseItemUsed;
        // Fires when a hit actually lands (not on a blocked/shielded hit) — stage systems
        // hook into this for stage-specific hit consequences (e.g. 비키니시티 "비법" drop).
        public event Action OnHitByAttackItem;
        // Fires when the car actively scrapes a wall (steering into it, not just grazing) —
        // 비키니시티 hooks into this so ramming the track boundary also drops "비법", same as
        // attack items / terrain hazards.
        public event Action OnWallHit;

        // --- Debug/test readouts ---
        public float CurrentSpeed => currentSpeed;
        public float CurrentAcceleration { get; private set; }
        // Time-window based rather than an Enter/Exit counter: physics contacts can flicker
        // apart and back together within the same collision, which desynced a counter.
        public bool IsColliding => Time.time - lastCollisionTime < collisionMemory;

        // --- Status-effect readouts (VehicleStatusHUD) ---
        // Plain read-only mirrors of the private state fields below — polled once a frame by
        // the HUD rather than adding a start/end event pair for each one, since none of these
        // need anything fancier than "currently on or off".
        public bool IsStunned => isStunned;
        public bool IsSteeringInverted => steeringInverted;
        public bool IsKnockedBack => isKnockedBack;
        public bool HasShield => hasShield;
        public bool HasSpeedBoost => itemSpeedBonus > 0f;
        public bool HasDriftBoost => driftSpeedBonus > 0f;

        Rigidbody rb;
        InputAction steerAction;
        InputAction throttleAction;
        InputAction driftAction;
        InputAction useAccelItemAction;
        InputAction useAttackDefenseItemAction;

        float currentSpeed;
        float itemSpeedBonus;
        float driftSpeedBonus;
        bool isStunned;
        bool inputLocked;
        bool steeringInverted;
        bool isKnockedBack;
        bool hasShield;
        bool isDrifting;
        float driftHoldTime;
        // Actual travel direction, as distinct from transform.forward (facing). Snaps to
        // forward every frame while not drifting (identical to the old always-aligned
        // behavior) — only lags behind facing while isDrifting, which is what produces the
        // sideways slide. Kept unit-length via RotateTowards with maxMagnitudeDelta=0.
        Vector3 moveDirection = Vector3.forward;
        Coroutine driftBoostRoutine;
        // A short outward shove that rides ON TOP of normal throttle/steering (added to the
        // driving velocity, then decayed) rather than seizing control the way ApplyKnockback
        // does — this is the "통통 튀는" bounce for the ghast fireball, so the player never
        // loses control on contact. Decays toward zero over a few frames.
        Vector3 bounceVelocity;
        Coroutine speedBoostRoutine;
        Coroutine stunRoutine;
        Coroutine steeringInvertRoutine;
        Coroutine knockbackRoutine;
        Coroutine shieldRoutine;
        float lastCollisionTime = -10f;
        const float collisionMemory = 0.15f;
        float lastWallHitEventTime = -Mathf.Infinity;

        // Wall-collision tracking: accumulated contact normal from the current physics step.
        // Reset at the top of FixedUpdate and rebuilt by OnCollisionStay. Used by
        // ApplyThrottle to slide along walls instead of pushing through them.
        Vector3 wallContactNormal;
        bool wallContactActive;
        // Small outward nudge applied per FixedUpdate when in contact with a wall, to help
        // PhysX resolve any residual penetration that the velocity-clamping alone doesn't fix.
        const float WallSeparationForce = 4f;

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

            driftAction = new InputAction("Drift", InputActionType.Button, "<Keyboard>/leftShift");

            useAccelItemAction = new InputAction("UseAccelItem", InputActionType.Button, "<Keyboard>/leftCtrl");
            useAttackDefenseItemAction = new InputAction("UseAttackDefenseItem", InputActionType.Button, "<Keyboard>/e");

            Vector3 initialForward = transform.forward;
            initialForward.y = 0f;
            moveDirection = initialForward.normalized;
        }

        void OnEnable()
        {
            steerAction.Enable();
            throttleAction.Enable();
            driftAction.Enable();
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
            driftAction.Disable();
            useAccelItemAction.Disable();
            useAttackDefenseItemAction.Disable();
        }

        void HandleAccelItemUsed(InputAction.CallbackContext ctx) => OnAccelItemUsed?.Invoke();
        void HandleAttackDefenseItemUsed(InputAction.CallbackContext ctx) => OnAttackDefenseItemUsed?.Invoke();

        void FixedUpdate()
        {
            float speedBeforeUpdate = currentSpeed;

            // Snapshot and reset wall-contact state for this physics step. OnCollisionStay
            // fires BEFORE FixedUpdate in Unity's execution order, so by now wallContactNormal
            // already holds the aggregate push-away direction from all active wall contacts
            // (or zero if there are none).
            bool touchingWall = wallContactActive;
            Vector3 wallNormal = wallContactNormal;
            wallContactActive = false;
            wallContactNormal = Vector3.zero;

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
                bool driftHeld = driftAction.IsPressed();

                if (driftHeld && !isDrifting)
                {
                    isDrifting = true;
                    driftHoldTime = 0f;
                }
                else if (!driftHeld && isDrifting)
                {
                    isDrifting = false;
                    if (driftHoldTime >= minDriftHoldTimeForBoost)
                    {
                        float chargeFraction = Mathf.Clamp01(driftHoldTime / driftBoostChargeTime);
                        ApplyDriftBoost(chargeFraction * maxDriftBoostSpeed, driftBoostDuration);
                    }
                    driftHoldTime = 0f;
                }

                if (isDrifting)
                {
                    driftHoldTime += Time.fixedDeltaTime;
                }

                ApplyThrottle(throttleInput, isDrifting, touchingWall, wallNormal);
                ApplySteering(steeringInverted ? -steerInput : steerInput, isDrifting);
            }

            CurrentAcceleration = (currentSpeed - speedBeforeUpdate) / Time.fixedDeltaTime;

            // Bleed the bounce shove back to zero so it's a quick nudge, not a lasting drift.
            bounceVelocity = Vector3.MoveTowards(bounceVelocity, Vector3.zero, bounceDecay * Time.fixedDeltaTime);
        }

        void OnCollisionEnter(Collision collision)
        {
            lastCollisionTime = Time.time;
            AccumulateWallContact(collision);
        }

        void OnCollisionStay(Collision collision)
        {
            lastCollisionTime = Time.time;
            AccumulateWallContact(collision);
        }

        // Builds an averaged push-away direction from all contact points on this wall.
        // Multiple walls (e.g. corners) accumulate into one combined normal per frame,
        // which ApplyThrottle uses to remove the into-wall velocity component.
        void AccumulateWallContact(Collision collision)
        {
            // Only care about objects explicitly marked with WallMarker. This avoids any false
            // positives from floor planes, terrain hazards, or improperly destroyed visual colliders.
            if (collision.collider.GetComponent<M2.Core.WallMarker>() == null &&
                collision.collider.GetComponentInParent<M2.Core.WallMarker>() == null)
            {
                return;
            }

            Vector3 avgNormal = Vector3.zero;
            for (int i = 0; i < collision.contactCount; i++)
            {
                avgNormal += collision.GetContact(i).normal;
            }
            if (avgNormal.sqrMagnitude < 0.0001f) return;

            avgNormal.y = 0f;
            avgNormal.Normalize();

            wallContactNormal = (wallContactNormal + avgNormal).normalized;
            wallContactActive = true;
        }

        void ApplyThrottle(float throttleInput, bool isDrifting, bool touchingWall, Vector3 wallNormal)
        {
            float effectiveMaxSpeed = maxSpeed + itemSpeedBonus + driftSpeedBonus;

            if (Mathf.Approximately(throttleInput, 0f))
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
            forward.Normalize();

            // While drifting, moveDirection lags behind the (faster-turning, see
            // ApplySteering) facing instead of snapping to it — that gap between where the
            // car points and where it's actually travelling is the slide. Not drifting means
            // instant snap, identical to the pre-drift always-aligned behavior.
            moveDirection = isDrifting
                ? Vector3.RotateTowards(moveDirection, forward, driftSlipRecoverySpeed * Mathf.Deg2Rad * Time.fixedDeltaTime, 0f)
                : forward;

            // bounceVelocity rides on top of throttle control (decayed in FixedUpdate) so a
            // fireball hit nudges the car outward without ever taking control away.
            Vector3 velocity = moveDirection * currentSpeed + bounceVelocity;

            // --- Wall-slide: prevent the script-set velocity from pushing into the wall ---
            // Without this, we overwrite rb.linearVelocity every frame with a vector that
            // points INTO the wall, which cancels PhysX's collision resolution and lets the
            // car tunnel straight through. By removing the into-wall component, the car
            // slides along the wall instead. currentSpeed is bled down (not zeroed) while
            // doing so — see wallScrapeDeceleration below — floored above minSpeedToSteer so
            // the car never gets stuck against the wall with no way to turn away (the exact
            // same "wedging" bug the old box-corner walls caused).
            if (touchingWall && wallNormal.sqrMagnitude > 0.01f)
            {
                float intoWall = Vector3.Dot(velocity, wallNormal);
                if (intoWall < 0f)
                {
                    // Remove the component pushing into the wall (project onto the wall plane).
                    velocity -= wallNormal * intoWall;

                    // Bleed currentSpeed while actually scraping the wall (steering into it),
                    // not just resting/grazing against it — otherwise the zero-friction
                    // PhysicsMaterial + pure direction-projection above means the car keeps
                    // 100% of its speed while sliding along a wall, which reads as an
                    // invisible rail rather than a collision. Floored above minSpeedToSteer so
                    // the player can always still steer away — dropping below that threshold
                    // is what caused the old "wedged with no steering" bug this wall-slide
                    // logic exists to avoid.
                    float speedFloor = minSpeedToSteer + 1f;
                    if (Mathf.Abs(currentSpeed) > speedFloor)
                    {
                        float speedSign = currentSpeed < 0f ? -1f : 1f;
                        currentSpeed = speedSign * Mathf.Max(speedFloor,
                            Mathf.Abs(currentSpeed) - wallScrapeDeceleration * Time.fixedDeltaTime);
                    }

                    if (Time.time - lastWallHitEventTime >= wallHitEventCooldown)
                    {
                        lastWallHitEventTime = Time.time;
                        OnWallHit?.Invoke();
                        // Same cooldown as the event above so this reads as one discrete bump
                        // per hit rather than a continuous shove while pressed into the wall —
                        // ApplyBounce/bounceDecay is the same never-loses-control mechanism
                        // GhastFireball uses, just much gentler here.
                        ApplyBounce(wallNormal * wallBounceStrength);
                    }
                }
                // Small nudge away from the wall to help resolve any residual overlap
                velocity += wallNormal * WallSeparationForce * Time.fixedDeltaTime;
            }

            velocity.y = rb.linearVelocity.y;
            rb.linearVelocity = velocity;
        }

        void ApplySteering(float steerInput, bool isDrifting)
        {
            if (Mathf.Abs(currentSpeed) < minSpeedToSteer) return;

            float speedFactor = Mathf.Abs(currentSpeed) / Mathf.Max(maxSpeed, 0.0001f);
            float turnFactor = Mathf.Lerp(1f, turnSpeedAtMaxVelocity, speedFactor);
            if (isDrifting) turnFactor *= driftTurnMultiplier;

            float speedSign = currentSpeed < 0f ? -1f : 1f;
            float yaw = steerInput * turnSpeed * turnFactor * speedSign * Time.fixedDeltaTime;
            Quaternion deltaRotation = Quaternion.Euler(0f, yaw, 0f);
            rb.MoveRotation(rb.rotation * deltaRotation);
        }

        // --- Drift boost (마리오카트식 미니터보) ---

        void ApplyDriftBoost(float bonusSpeed, float duration)
        {
            if (driftBoostRoutine != null) StopCoroutine(driftBoostRoutine);
            driftBoostRoutine = StartCoroutine(DriftBoostRoutine(bonusSpeed, duration));
        }

        IEnumerator DriftBoostRoutine(float bonusSpeed, float duration)
        {
            driftSpeedBonus = bonusSpeed;
            yield return new WaitForSeconds(duration);
            driftSpeedBonus = 0f;
            driftBoostRoutine = null;
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

        // Lighter alternative to ApplyKnockback: a "통통 튀는" bounce that shoves the car
        // outward but keeps the player in full control the whole time (the impulse is added
        // on top of throttle in ApplyThrottle and decays over a few frames). Used by the ghast
        // fireball, which previously froze the car with ApplyKnockback — reported as "먹통".
        public void ApplyBounce(Vector3 impulse)
        {
            impulse.y = 0f;
            bounceVelocity = impulse;
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
