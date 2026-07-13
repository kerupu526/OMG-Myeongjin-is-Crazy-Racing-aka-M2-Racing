using System.Collections;
using M2.Items;
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

        // Server-written, everyone-read (default write permission = Server). None = empty.
        readonly NetworkVariable<byte> netPrimary = new NetworkVariable<byte>((byte)NetItemId.None);
        readonly NetworkVariable<byte> netSecondary = new NetworkVariable<byte>((byte)NetItemId.None);

        public NetItemId Primary => (NetItemId)netPrimary.Value;
        public NetItemId Secondary => (NetItemId)netSecondary.Value;

        VehicleController vehicleController;

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
            }
        }

        public override void OnNetworkDespawn()
        {
            // Unconditional unsubscribe (no-op if never subscribed) — safe even if ownership moved.
            vehicleController.OnAccelItemUsed -= HandleAccelItemUsed;
            vehicleController.OnAttackDefenseItemUsed -= HandleAttackDefenseItemUsed;
        }

        // ---- Server: inventory ----

        // Called by the spawn manager when this vehicle collects a pickup. Applies the same
        // fill/replace rule the local ItemSlots.CollectItem uses.
        public void ServerCollect(NetItemId id)
        {
            if (!IsServer || id == NetItemId.None) return;

            ApplyCollect(Primary, Secondary, id, out NetItemId newPrimary, out NetItemId newSecondary);
            netPrimary.Value = (byte)newPrimary;
            netSecondary.Value = (byte)newSecondary;
        }

        void ClearSlot(ItemSlotChoice choice)
        {
            if (choice == ItemSlotChoice.Primary) netPrimary.Value = (byte)NetItemId.None;
            else if (choice == ItemSlotChoice.Secondary) netSecondary.Value = (byte)NetItemId.None;
        }

        // ---- Owner: input -> server ----

        void HandleAccelItemUsed() => UseAccelItemRpc();
        void HandleAttackDefenseItemUsed() => UseAttackDefenseItemRpc();

        [Rpc(SendTo.Server)]
        void UseAccelItemRpc()
        {
            ItemSlotChoice choice = SelectAccelSlot(Primary, Secondary);
            if (choice == ItemSlotChoice.None) return; // stale/empty press

            NetItemId used = choice == ItemSlotChoice.Primary ? Primary : Secondary;
            ClearSlot(choice);

            ItemDefinition def = ItemCatalog.CreateFromId(used);
            ApplySpeedBoostOwnerRpc(def.speedBonus, def.duration);
            Debug.Log($"M2Net: 클라이언트 {OwnerClientId} 가속 아이템({used}) 사용.");
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
                SpawnBombVisualRpc(pos, (byte)used);
                StartCoroutine(ResolveBombServer(pos, def));
                Debug.Log($"M2Net: 클라이언트 {OwnerClientId} 공격 아이템({used}) 설치.");
            }
            else
            {
                // Shield state is authoritative on the host's synced copy (checked at explosion
                // time), and mirrored to the owner's copy for its HUD.
                vehicleController.ActivateShield(def.duration);
                ActivateShieldOwnerRpc(def.duration);
                Debug.Log($"M2Net: 클라이언트 {OwnerClientId} 방어 아이템({used}) 사용.");
            }
        }

        // ---- Server: authoritative bomb explosion ----

        IEnumerator ResolveBombServer(Vector3 position, ItemDefinition def)
        {
            yield return new WaitForSeconds(def.armTime);

            foreach (Collider hit in Physics.OverlapSphere(position, def.attackRadius))
            {
                if (!hit.CompareTag("Player")) continue;

                NetworkItemSlots victim = hit.GetComponentInParent<NetworkItemSlots>();
                if (victim == null) continue;

                // TryConsumeShield reads/consumes the shield on the host's copy of the victim — the
                // single authoritative shield state (kept in sync by ActivateShield above running on
                // that same host copy). No owner exclusion, matching the local BombRunner (a placer
                // can catch its own blast).
                if (victim.vehicleController.TryConsumeShield())
                {
                    victim.ConsumeShieldOwnerRpc();
                    Debug.Log($"M2Net: 폭탄 명중했으나 클라이언트 {victim.OwnerClientId}가 방패로 막음.");
                }
                else
                {
                    victim.ApplyHitStunOwnerRpc(victim.bombStunDuration);
                    Debug.Log($"M2Net: [서버] 폭탄이 클라이언트 {victim.OwnerClientId}를 기절시킴 — 오너에게 RPC 전송.");
                }
            }
        }

        // ---- Cosmetic bomb marker on every peer (host included) ----

        [Rpc(SendTo.Everyone)]
        void SpawnBombVisualRpc(Vector3 position, byte itemId)
        {
            ItemDefinition def = ItemCatalog.CreateFromId((NetItemId)itemId);
            if (def == null) return;

            GameObject marker = ItemEffects.SpawnBombVisual(position, def);
            StartCoroutine(DestroyAfter(marker, def.armTime));
        }

        static IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null) Destroy(go);
        }

        // ---- Owner-side effect application (physics/HUD resolve where the car is simulated) ----
        // Each logs on the owner so a live test can confirm the effect actually reached the car
        // that must simulate it (not just that the server sent it). Remove these with the rest of
        // the M2Net diagnostics once the milestone is signed off.

        [Rpc(SendTo.Owner)]
        void ApplySpeedBoostOwnerRpc(float bonus, float duration)
        {
            Debug.Log($"M2Net: [오너 {OwnerClientId}] 가속 부스트 적용 (+{bonus}, {duration}s).");
            vehicleController.ApplySpeedBoost(bonus, duration);
        }

        [Rpc(SendTo.Owner)]
        void ActivateShieldOwnerRpc(float duration)
        {
            Debug.Log($"M2Net: [오너 {OwnerClientId}] 방어막 적용 ({duration}s).");
            vehicleController.ActivateShield(duration);
        }

        [Rpc(SendTo.Owner)]
        void ApplyHitStunOwnerRpc(float duration)
        {
            Debug.Log($"M2Net: [오너 {OwnerClientId}] 기절 적용 ({duration}s) — 차량 정지.");
            vehicleController.ApplyHitStun(duration);
        }

        // Keeps the owner's HasShield HUD mirror in step with the host consuming the shield.
        [Rpc(SendTo.Owner)]
        void ConsumeShieldOwnerRpc()
        {
            Debug.Log($"M2Net: [오너 {OwnerClientId}] 방패 소모(피격 방어).");
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
