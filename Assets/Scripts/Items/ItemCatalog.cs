using UnityEngine;

namespace M2.Items
{
    // Base-tier items only (priority 2 scope: spawn/slot/use/hit-effect framework).
    // The full roster from CLAUDE.md (재석 유, 원자폭탄, C4, 황금방패, 막대형 수류탄,
    // 명진이의 러브레터 등) is reserved for the priority 5 item-roster expansion pass.
    public static class ItemCatalog
    {
        const float DerivedUpgradeChance = 0.1f;

        public static ItemDefinition CreateAccelBase() => new ItemDefinition
        {
            itemName = "휘발유",
            type = ItemType.Accel,
            tier = 0,
            duration = 2.0f,
            speedBonus = 20f
        };

        public static ItemDefinition CreateAccelDerived() => new ItemDefinition
        {
            itemName = "슈퍼 휘발유",
            type = ItemType.Accel,
            tier = 1,
            duration = 2.0f,
            speedBonus = 35f
        };

        public static ItemDefinition CreateAttackBase() => new ItemDefinition
        {
            itemName = "폭탄",
            type = ItemType.Attack,
            tier = 0,
            armTime = 3.0f,
            attackRadius = 5f
        };

        public static ItemDefinition CreateAttackDerived() => new ItemDefinition
        {
            itemName = "다이너마이트",
            type = ItemType.Attack,
            tier = 1,
            armTime = 4.0f,
            attackRadius = 10f
        };

        public static ItemDefinition CreateDefenseBase() => new ItemDefinition
        {
            itemName = "방패",
            type = ItemType.Defense,
            tier = 0,
            duration = 4.0f
        };

        public static ItemDefinition CreateDefenseDerived() => new ItemDefinition
        {
            itemName = "가시 방패",
            type = ItemType.Defense,
            tier = 1,
            duration = 4.0f
        };

        // CLAUDE.md spawn rule: type is chosen uniformly at random, then a single 10%
        // roll (at spawn time only, not periodic) decides whether it's the derived form.
        public static ItemDefinition CreateRandomForSpawn()
        {
            var type = (ItemType)Random.Range(0, 3);
            bool upgraded = Random.value < DerivedUpgradeChance;

            return type switch
            {
                ItemType.Accel => upgraded ? CreateAccelDerived() : CreateAccelBase(),
                ItemType.Attack => upgraded ? CreateAttackDerived() : CreateAttackBase(),
                _ => upgraded ? CreateDefenseDerived() : CreateDefenseBase(),
            };
        }
    }
}
