using System;

namespace M2.Items
{
    [Serializable]
    public class ItemDefinition
    {
        public NetItemId id;
        public string itemName;
        public string description;
        public string artKey;
        public ItemType type;
        public int tier;
        public ItemBehavior behavior;
        public ShieldStrength shieldStrength;
        public bool heartEffect;

        // Accel: boost duration (s) / speed bonus. Defense: shield duration (s).
        public float duration;
        public float speedBonus;

        // Attack: delay before it explodes (s) / explosion radius (m).
        public float armTime;
        public float attackRadius;
        public float triggerDistance;
    }

    public enum ItemBehavior
    {
        SpeedBoost,
        TimedBomb,
        RemoteC4,
        ProximityGrenade,
        AtomicBomb,
        Shield,
    }

    public enum ShieldStrength
    {
        None,
        Basic,
        Spiked,
        Golden,
    }
}
