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
            return CreateForType(type, upgraded);
        }

        static ItemDefinition CreateForType(ItemType type, bool upgraded)
        {
            return type switch
            {
                ItemType.Accel => upgraded ? CreateAccelDerived() : CreateAccelBase(),
                ItemType.Attack => upgraded ? CreateAttackDerived() : CreateAttackBase(),
                _ => upgraded ? CreateDefenseDerived() : CreateDefenseBase(),
            };
        }

        // --- Netcode (Milestone 2b) helpers ---
        // The wire ID and the full definition are kept in sync purely by arithmetic:
        // id = type * 2 + tier + 1. Only the byte ID crosses the network; every peer
        // rebuilds the ItemDefinition locally so names/stats never travel.

        public static NetItemId IdFor(ItemType type, int tier)
        {
            return (NetItemId)((int)type * 2 + tier + 1);
        }

        // Inverse of the IdFor layout (id = type*2 + tier + 1): type = (id - 1) / 2.
        // Only meaningful for a real item (id != None); callers guard None themselves.
        public static ItemType TypeOf(NetItemId id)
        {
            return (ItemType)(((int)id - 1) / 2);
        }

        // Server-only: same uniform-type + 10%-derived roll as CreateRandomForSpawn,
        // but returns the compact ID the server writes into replicated spawn state.
        public static NetItemId CreateRandomIdForSpawn()
        {
            var type = (ItemType)Random.Range(0, 3);
            int tier = Random.value < DerivedUpgradeChance ? 1 : 0;
            return IdFor(type, tier);
        }

        // Rebuilds the full definition from a replicated ID. None -> null (empty slot).
        public static ItemDefinition CreateFromId(NetItemId id)
        {
            return id switch
            {
                NetItemId.AccelBase => CreateAccelBase(),
                NetItemId.AccelDerived => CreateAccelDerived(),
                NetItemId.AttackBase => CreateAttackBase(),
                NetItemId.AttackDerived => CreateAttackDerived(),
                NetItemId.DefenseBase => CreateDefenseBase(),
                NetItemId.DefenseDerived => CreateDefenseDerived(),
                _ => null,
            };
        }
    }
}
