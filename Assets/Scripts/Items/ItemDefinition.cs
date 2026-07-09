using System;

namespace M2.Items
{
    [Serializable]
    public class ItemDefinition
    {
        public string itemName;
        public ItemType type;
        public int tier;

        // Accel: boost duration (s) / speed bonus. Defense: shield duration (s).
        public float duration;
        public float speedBonus;

        // Attack: delay before it explodes (s) / explosion radius (m).
        public float armTime;
        public float attackRadius;
    }
}
