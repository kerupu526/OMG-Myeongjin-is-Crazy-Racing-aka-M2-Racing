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
    // Milestone 2b step 3 scope: state only (collect + replicated slots + the pure fill/select
    // rules). Item USE (input -> ServerRpc -> effect Owner/Everyone RPCs) is wired in step 5.
    [RequireComponent(typeof(VehicleController))]
    public class NetworkItemSlots : NetworkBehaviour
    {
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
