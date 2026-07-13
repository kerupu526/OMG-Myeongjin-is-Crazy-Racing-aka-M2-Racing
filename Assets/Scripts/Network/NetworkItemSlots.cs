using System.Collections;
using System.Collections.Generic;
using M2.Items;
using M2.Core;
using M2.Player;
using Unity.Netcode;
using UnityEngine;

namespace M2.Network
{
    public enum ItemSlotChoice { None, Primary, Secondary }

    // Networked replacement for the local ItemSlots. In online play the vehicle prefab carries
    // this instead of ItemSlots. Slot contents are server-authoritative and replicated as compact
    // NetItemId bytes: the host uses them to validate item use (it holds both cars' synced
    // copies), and the owning client reads them for its HUD.
    //
    // Authority split for USE (consistent with owner-authoritative movement + host-authoritative
    // race state):
    //   - Input happens on the owning client, which asks the server to use an item (ServerRpc).
    //   - The server validates against the replicated slots, clears the slot, and applies effects:
    //       * Accel  -> Owner RPC so the OWNER runs ApplySpeedBoost (owner-authoritative physics;
    //                   the speed change replicates out through NetworkTransform).
    //       * Defense -> ActivateShield on the host's synced copy (authoritative shield state the
    //                   host checks at explosion time) + an Owner RPC so the owner's copy mirrors
    //                   HasShield for its HUD.
    //       * Attack  -> an Everyone RPC spawns the cosmetic bomb on all peers, while the host runs
    //                   the single authoritative OverlapSphere and, per victim, either consumes a
    //                   shield or stuns (both delivered to the victim's OWNER so the stun/physics
    //                   resolve where that car is actually simulated).
    [RequireComponent(typeof(VehicleController))]
    public class NetworkItemSlots : NetworkBehaviour
    {
        [Tooltip("폭탄 피격 시 기절(정지) 지속시간(초). 로컬 기본 0.6초보다 살짝 길게 잡아 온라인에서도 확실히 보이게.")]
        public float bombStunDuration = 1.0f;

        [Tooltip("기본 폭탄을 사용하지 않고 보유하면 원자폭탄으로 변하는 시간(초).")]
        public float atomicUpgradeDelay = 150f;

        // Server-written, everyone-read (default write permission = Server). None = empty.
        readonly NetworkVariable<byte> netPrimary = new NetworkVariable<byte>((byte)NetItemId.None);
        readonly NetworkVariable<byte> netSecondary = new NetworkVariable<byte>((byte)NetItemId.None);

        public NetItemId Primary => (NetItemId)netPrimary.Value;
        public NetItemId Secondary => (NetItemId)netSecondary.Value;

        VehicleController vehicleController;
        readonly List<Vector3> serverC4Positions = new List<Vector3>();
        readonly List<AttackVisual> attackVisuals = new List<AttackVisual>();
        float primaryHeldTime;
        float secondaryHeldTime;

        class AttackVisual
        {
            public NetItemId id;
            public Vector3 position;
            public GameObject gameObject;
        }

        void Awake()
        {
            vehicleController = GetComponent<VehicleController>();
        }

        public override void OnNetworkSpawn()
        {
            // Only the owning client reads local input; it forwards use requests to the server.
            if (IsOwner)
            {
                vehicleController.OnAccelItemUsed += HandleAccelItemUsed;
                vehicleController.OnAttackDefenseItemUsed += HandleAttackDefenseItemUsed;
                vehicleController.OnRemoteItemTriggered += HandleRemoteItemTriggered;
            }
        }

        public override void OnNetworkDespawn()
        {
            // Unconditional unsubscribe (no-op if never subscribed) — safe even if ownership moved.
            vehicleController.OnAccelItemUsed -= HandleAccelItemUsed;
            vehicleController.OnAttackDefenseItemUsed -= HandleAttackDefenseItemUsed;
            vehicleController.OnRemoteItemTriggered -= HandleRemoteItemTriggered;
        }

        void Update()
        {
            if (!IsServer || !IsSpawned) return;
            TickAtomicUpgrade(netPrimary, ref primaryHeldTime);
            TickAtomicUpgrade(netSecondary, ref secondaryHeldTime);
        }

        void TickAtomicUpgrade(NetworkVariable<byte> slot, ref float heldTime)
        {
            if ((NetItemId)slot.Value != NetItemId.Bomb)
            {
                heldTime = 0f;
                return;
            }
            heldTime += Time.deltaTime;
            if (heldTime < atomicUpgradeDelay) return;
            slot.Value = (byte)NetItemId.AtomicBomb;
            heldTime = 0f;
        }

        // ---- Server: inventory ----

        // Called by the spawn manager when this vehicle collects a pickup. Applies the same
        // fill/replace rule the local ItemSlots.CollectItem uses.
        public void ServerCollect(NetItemId id)
        {
            if (!IsServer || id == NetItemId.None) return;

            ApplyCollect(Primary, Secondary, id, out NetItemId newPrimary, out NetItemId newSecondary);
            bool primaryChanged = newPrimary != Primary;
            bool secondaryChanged = newSecondary != Secondary;
            netPrimary.Value = (byte)newPrimary;
            netSecondary.Value = (byte)newSecondary;
            if (primaryChanged) primaryHeldTime = 0f;
            if (secondaryChanged) secondaryHeldTime = 0f;
        }

        void ClearSlot(ItemSlotChoice choice)
        {
            if (choice == ItemSlotChoice.Primary) netPrimary.Value = (byte)NetItemId.None;
            else if (choice == ItemSlotChoice.Secondary) netSecondary.Value = (byte)NetItemId.None;
        }

        // ---- Owner: input -> server ----

        void HandleAccelItemUsed() => UseAccelItemRpc();
        void HandleAttackDefenseItemUsed() => UseAttackDefenseItemRpc();
        void HandleRemoteItemTriggered() => DetonateC4Rpc();

        [Rpc(SendTo.Server)]
        void UseAccelItemRpc()
        {
            ItemSlotChoice choice = SelectAccelSlot(Primary, Secondary);
            if (choice == ItemSlotChoice.None) return; // stale/empty press

            NetItemId used = choice == ItemSlotChoice.Primary ? Primary : Secondary;
            ClearSlot(choice);

            ItemDefinition def = ItemCatalog.CreateFromId(used);
            ApplySpeedBoostOwnerRpc(def.speedBonus, def.duration);
        }

        [Rpc(SendTo.Server)]
        void UseAttackDefenseItemRpc()
        {
            ItemSlotChoice choice = SelectAttackDefenseSlot(Primary, Secondary);
            if (choice == ItemSlotChoice.None) return; // stale/empty press

            NetItemId used = choice == ItemSlotChoice.Primary ? Primary : Secondary;
            ClearSlot(choice);

            ItemDefinition def = ItemCatalog.CreateFromId(used);
            if (def.type == ItemType.Attack)
            {
                Vector3 pos = transform.position;
                SpawnAttackVisualRpc(pos, (byte)used);
                switch (def.behavior)
                {
                    case ItemBehavior.RemoteC4:
                        serverC4Positions.Add(pos);
                        break;
                    case ItemBehavior.ProximityGrenade:
                        StartCoroutine(ResolveProximityServer(pos, def));
                        break;
                    default:
                        StartCoroutine(ResolveAttackServer(pos, def));
                        break;
                }
            }
            else
            {
                // Shield state is authoritative on the host's synced copy (checked at explosion
                // time), and mirrored to the owner's copy for its HUD.
                vehicleController.ActivateShield(def.duration, def.shieldStrength);
                ActivateShieldOwnerRpc(def.duration, (byte)def.shieldStrength);
            }
        }

        [Rpc(SendTo.Server)]
        void DetonateC4Rpc()
        {
            ItemDefinition def = ItemCatalog.CreateFromId(NetItemId.C4);
            foreach (Vector3 position in serverC4Positions)
            {
                ResolveAttackAtServer(position, def);
                RemoveAttackVisualRpc((byte)NetItemId.C4, position);
            }
            serverC4Positions.Clear();
        }

        // ---- Server: authoritative attack resolution ----

        IEnumerator ResolveAttackServer(Vector3 position, ItemDefinition def)
        {
            if (def.armTime > 0f) yield return new WaitForSeconds(def.armTime);
            ResolveAttackAtServer(position, def);
            if (def.heartEffect) SpawnHeartEffectRpc(position);
            if (def.behavior != ItemBehavior.TimedBomb)
                RemoveAttackVisualRpc((byte)def.id, position);
        }

        IEnumerator ResolveProximityServer(Vector3 position, ItemDefinition def)
        {
            while (!HasOpponentNear(position, def.triggerDistance)) yield return new WaitForFixedUpdate();
            ResolveAttackAtServer(position, def);
            RemoveAttackVisualRpc((byte)def.id, position);
        }

        bool HasOpponentNear(Vector3 position, float distance)
        {
            foreach (Collider hit in Physics.OverlapSphere(position, distance))
            {
                NetworkItemSlots other = hit.GetComponentInParent<NetworkItemSlots>();
                if (other != null && other != this) return true;
            }
            return false;
        }

        void ResolveAttackAtServer(Vector3 position, ItemDefinition def)
        {
            var resolved = new HashSet<NetworkItemSlots>();
            foreach (Collider hit in Physics.OverlapSphere(position, def.attackRadius))
            {
                if (!hit.CompareTag("Player")) continue;

                NetworkItemSlots victim = hit.GetComponentInParent<NetworkItemSlots>();
                if (victim == null || !resolved.Add(victim)) continue;

                // TryConsumeShield reads/consumes the shield on the host's copy of the victim — the
                // single authoritative shield state (kept in sync by ActivateShield above running on
                // that same host copy). No owner exclusion, matching the local BombRunner (a placer
                // can catch its own blast).
                if (victim.vehicleController.TryBlockAttack(def, out bool reflected))
                {
                    victim.ConsumeShieldOwnerRpc();
                    if (reflected && victim != this) ApplyHitStunOwnerRpc(bombStunDuration);
                }
                else
                {
                    victim.TriggerChargingBombServer();
                    float duration = def.behavior == ItemBehavior.AtomicBomb ? 3f : victim.bombStunDuration;
                    victim.ApplyHitStunOwnerRpc(duration);
                }
            }

            if (def.behavior == ItemBehavior.AtomicBomb)
                FindFirstObjectByType<GameManager>()?.EndRaceAsDraw("원자폭탄");
        }

        void TriggerChargingBombServer()
        {
            ItemSlotChoice choice = Primary == NetItemId.Bomb
                ? ItemSlotChoice.Primary
                : Secondary == NetItemId.Bomb ? ItemSlotChoice.Secondary : ItemSlotChoice.None;
            if (choice == ItemSlotChoice.None) return;

            ClearSlot(choice);
            if (choice == ItemSlotChoice.Primary) primaryHeldTime = 0f;
            else secondaryHeldTime = 0f;

            ItemDefinition atomic = ItemCatalog.CreateFromId(NetItemId.AtomicBomb);
            Vector3 position = transform.position;
            SpawnAttackVisualRpc(position, (byte)atomic.id);
            ResolveAttackAtServer(position, atomic);
            RemoveAttackVisualRpc((byte)atomic.id, position);
        }

        // ---- Cosmetic bomb marker on every peer (host included) ----

        [Rpc(SendTo.Everyone)]
        void SpawnAttackVisualRpc(Vector3 position, byte itemId)
        {
            ItemDefinition def = ItemCatalog.CreateFromId((NetItemId)itemId);
            if (def == null) return;

            GameObject marker = ItemEffects.SpawnBombVisual(position, def);
            if (def.behavior == ItemBehavior.TimedBomb)
            {
                StartCoroutine(DestroyAfter(marker, def.armTime));
            }
            else
            {
                attackVisuals.Add(new AttackVisual { id = def.id, position = position, gameObject = marker });
            }
        }

        [Rpc(SendTo.Everyone)]
        void RemoveAttackVisualRpc(byte itemId, Vector3 position)
        {
            NetItemId id = (NetItemId)itemId;
            for (int i = 0; i < attackVisuals.Count; i++)
            {
                AttackVisual visual = attackVisuals[i];
                if (visual.id != id || Vector3.SqrMagnitude(visual.position - position) > 0.01f) continue;
                if (visual.gameObject != null) Destroy(visual.gameObject);
                attackVisuals.RemoveAt(i);
                break;
            }

            ItemDefinition def = ItemCatalog.CreateFromId(id);
            if (def != null && def.heartEffect) ItemEffects.SpawnHeartEffect(position);
        }

        [Rpc(SendTo.Everyone)]
        void SpawnHeartEffectRpc(Vector3 position) => ItemEffects.SpawnHeartEffect(position);

        static IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null) Destroy(go);
        }

        // ---- Owner-side effect application (physics/HUD resolve where the car is simulated) ----

        [Rpc(SendTo.Owner)]
        void ApplySpeedBoostOwnerRpc(float bonus, float duration)
        {
            vehicleController.ApplySpeedBoost(bonus, duration);
        }

        [Rpc(SendTo.Owner)]
        void ActivateShieldOwnerRpc(float duration, byte strength)
        {
            vehicleController.ActivateShield(duration, (ShieldStrength)strength);
        }

        [Rpc(SendTo.Owner)]
        void ApplyHitStunOwnerRpc(float duration)
        {
            vehicleController.ApplyHitStun(duration);
        }

        // Keeps the owner's HasShield HUD mirror in step with the host consuming the shield.
        [Rpc(SendTo.Owner)]
        void ConsumeShieldOwnerRpc()
        {
            vehicleController.TryConsumeShield();
        }

        // ---- Pure rules (no NetworkManager needed — PlayMode-tested directly) ----

        // Fill primary, then secondary, then (both full) replace primary — matching
        // ItemSlots.CollectItem exactly.
        public static void ApplyCollect(NetItemId primary, NetItemId secondary, NetItemId incoming,
            out NetItemId newPrimary, out NetItemId newSecondary)
        {
            newPrimary = primary;
            newSecondary = secondary;

            if (primary == NetItemId.None) newPrimary = incoming;
            else if (secondary == NetItemId.None) newSecondary = incoming;
            else newPrimary = incoming;
        }

        // Ctrl consumes the first slot holding an Accel item (primary checked first) — mirrors
        // ItemSlots.UseAccelItem.
        public static ItemSlotChoice SelectAccelSlot(NetItemId primary, NetItemId secondary)
        {
            if (primary != NetItemId.None && ItemCatalog.TypeOf(primary) == ItemType.Accel) return ItemSlotChoice.Primary;
            if (secondary != NetItemId.None && ItemCatalog.TypeOf(secondary) == ItemType.Accel) return ItemSlotChoice.Secondary;
            return ItemSlotChoice.None;
        }

        // E consumes the first non-Accel item (primary preferred) — mirrors
        // ItemSlots.UseAttackDefenseItem.
        public static ItemSlotChoice SelectAttackDefenseSlot(NetItemId primary, NetItemId secondary)
        {
            if (primary != NetItemId.None && ItemCatalog.TypeOf(primary) != ItemType.Accel) return ItemSlotChoice.Primary;
            if (secondary != NetItemId.None && ItemCatalog.TypeOf(secondary) != ItemType.Accel) return ItemSlotChoice.Secondary;
            return ItemSlotChoice.None;
        }
    }
}
