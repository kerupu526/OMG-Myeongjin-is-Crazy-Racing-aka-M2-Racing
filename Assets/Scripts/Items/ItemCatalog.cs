using System.Collections.Generic;
using UnityEngine;

namespace M2.Items
{
    /// <summary>Canonical data from CLAUDE.md and 아이템 상세.pdf.</summary>
    public static class ItemCatalog
    {
        public const float DerivedUpgradeChance = 0.1f;

        static readonly NetItemId[] AccelDerivedPool =
        {
            NetItemId.SuperGasoline, NetItemId.HappyBirthdayToYou, NetItemId.JaeSeokGasoline
        };
        static readonly NetItemId[] AttackDerivedPool =
        {
            NetItemId.C4, NetItemId.Dynamite, NetItemId.StickGrenade,
            NetItemId.AtomicBomb, NetItemId.LoveLetter
        };
        static readonly NetItemId[] DefenseDerivedPool =
        {
            NetItemId.SpikedShield, NetItemId.GoldenShield
        };

        public static readonly NetItemId[] AllIds =
        {
            NetItemId.Gasoline, NetItemId.SuperGasoline, NetItemId.HappyBirthdayToYou,
            NetItemId.JaeSeokGasoline, NetItemId.Bomb, NetItemId.C4, NetItemId.Dynamite,
            NetItemId.StickGrenade, NetItemId.AtomicBomb, NetItemId.LoveLetter,
            NetItemId.Shield, NetItemId.SpikedShield, NetItemId.GoldenShield
        };

        public static ItemDefinition CreateAccelBase() => CreateFromId(NetItemId.Gasoline);
        public static ItemDefinition CreateAccelDerived() => CreateFromId(NetItemId.SuperGasoline);
        public static ItemDefinition CreateAttackBase() => CreateFromId(NetItemId.Bomb);
        public static ItemDefinition CreateAttackDerived() => CreateFromId(NetItemId.Dynamite);
        public static ItemDefinition CreateDefenseBase() => CreateFromId(NetItemId.Shield);
        public static ItemDefinition CreateDefenseDerived() => CreateFromId(NetItemId.SpikedShield);

        public static ItemDefinition CreateRandomForSpawn() => CreateFromId(CreateRandomIdForSpawn());

        public static NetItemId CreateRandomIdForSpawn()
        {
            ItemType type = (ItemType)Random.Range(0, 3);
            if (Random.value >= DerivedUpgradeChance)
            {
                return BaseId(type);
            }

            NetItemId[] pool = DerivedPool(type);
            return pool[Random.Range(0, pool.Length)];
        }

        public static ItemType TypeOf(NetItemId id)
        {
            byte value = (byte)id;
            if (value >= 1 && value <= 4) return ItemType.Accel;
            if (value >= 16 && value <= 21) return ItemType.Attack;
            if (value >= 32 && value <= 34) return ItemType.Defense;
            throw new KeyNotFoundException($"Unknown item id: {id} ({value})");
        }

        public static bool IsBase(NetItemId id) => id == NetItemId.Gasoline ||
            id == NetItemId.Bomb || id == NetItemId.Shield;

        static NetItemId BaseId(ItemType type) => type switch
        {
            ItemType.Accel => NetItemId.Gasoline,
            ItemType.Attack => NetItemId.Bomb,
            _ => NetItemId.Shield,
        };

        static NetItemId[] DerivedPool(ItemType type) => type switch
        {
            ItemType.Accel => AccelDerivedPool,
            ItemType.Attack => AttackDerivedPool,
            _ => DefenseDerivedPool,
        };

        public static ItemDefinition CreateFromId(NetItemId id)
        {
            return id switch
            {
                NetItemId.Gasoline => Accel(id, "휘발유", "Gasoline", 0, 2f, 20f,
                    "차량의 속도를 소폭 높여주는 차량용 휘발유입니다. 적당한 속도의 가속에 도움이 됩니다."),
                NetItemId.SuperGasoline => Accel(id, "슈퍼 휘발유", "Super Gasoline", 1, 2f, 35f,
                    "차량의 속도를 대폭 높여주는 차량용 휘발유입니다. 빠른 속도의 가속에 도움이 됩니다. 어째서인지 윤활유가 소량 섞여있는 것 같습니다..."),
                NetItemId.HappyBirthdayToYou => Accel(id, "해피버스데이투유", "HBD Gasoline", 1, 4f, 0f,
                    "차량의 속도를 전혀 높여주지 않는 생일용 휘발유입니다. 타인의 생일을 축하해주는 데에 도움이 됩니다."),
                NetItemId.JaeSeokGasoline => Accel(id, "재석 유", "Jae-seok Gasoline", 2, 1f, 100f,
                    "국민 MC인 유느님께서 직접 하사하신 휘발유입니다. 엄청 빠르게 달리고 싶을 때 사용합니다. 제품은 불스원샷으로 추정됩니다..."),

                NetItemId.Bomb => Attack(id, "폭탄", "bomb", 0, ItemBehavior.TimedBomb, 3f, 5f,
                    "상대방의 차량을 고장내기 위한 폭탄입니다. 상대방을 제압할 때 유용합니다."),
                NetItemId.C4 => Attack(id, "C4", "c4_bomb", 1, ItemBehavior.RemoteC4, -1f, 8f,
                    "상대방을 정확한 타이밍에 터트리기 위한 폭탄입니다. P 키를 눌러 C4를 터트릴 수 있으며 직격한 플레이어는 즉시 기절합니다."),
                NetItemId.Dynamite => Attack(id, "다이너마이트", "dynamite", 1, ItemBehavior.TimedBomb, 4f, 10f,
                    "일반 폭탄보다 조금 더 강력한 폭탄입니다. 상대를 크게 제압할 때 유용합니다."),
                NetItemId.StickGrenade => Attack(id, "막대형 수류탄", "stick_grenade", 1, ItemBehavior.ProximityGrenade, 0f, 10f,
                    "상대에게 근접해 사용하는 수류탄입니다. 상대의 움직임을 방해하는 데 유용합니다."),
                NetItemId.AtomicBomb => Attack(id, "원자폭탄", "atomic_bomb", 2, ItemBehavior.AtomicBomb, 0f, 10000f,
                    "가장 강력한 폭탄입니다. 사용 시 즉시 반경 10km 내의 모든 생명체와 지형이 제거됩니다. 게임을 끝내고 싶다면 이 폭탄을 사용하세요."),
                NetItemId.LoveLetter => Attack(id, "명진이의 러브레터", "myeongjin_love_letter", 1, ItemBehavior.TimedBomb, 3f, 5f,
                    "일반 폭탄의 특수 스킨입니다. 상대방의 차량을 고장내기 위한 폭탄이며 피격 시 하트 효과가 나타납니다.", true),

                NetItemId.Shield => Defense(id, "방패", "shield", 0, 4f, ShieldStrength.Basic,
                    "폭탄을 방어할 수 있는 일회성 방패를 소환합니다. 지속 시간이 지난 후 사라집니다."),
                NetItemId.SpikedShield => Defense(id, "가시 방패", "spike_shield", 1, 4f, ShieldStrength.Spiked,
                    "폭탄과 다이너마이트를 방어하고 다이너마이트를 반사할 수 있는 일회성 방패입니다."),
                NetItemId.GoldenShield => Defense(id, "황금방패", "shield", 2, 1f, ShieldStrength.Golden,
                    "즉사 공격을 제외한 모든 공격을 방어할 수 있는 일회성 방패입니다."),
                _ => null,
            };
        }

        static ItemDefinition Accel(NetItemId id, string name, string artKey, int tier,
            float duration, float speedBonus, string description) => new ItemDefinition
        {
            id = id, itemName = name, artKey = artKey, description = description,
            type = ItemType.Accel, tier = tier, behavior = ItemBehavior.SpeedBoost,
            duration = duration, speedBonus = speedBonus
        };

        static ItemDefinition Attack(NetItemId id, string name, string artKey, int tier,
            ItemBehavior behavior, float armTime, float radius, string description,
            bool heartEffect = false) => new ItemDefinition
        {
            id = id, itemName = name, artKey = artKey, description = description,
            type = ItemType.Attack, tier = tier, behavior = behavior,
            armTime = armTime, attackRadius = radius,
            triggerDistance = behavior == ItemBehavior.ProximityGrenade ? 5f : 0f,
            heartEffect = heartEffect
        };

        static ItemDefinition Defense(NetItemId id, string name, string artKey, int tier,
            float duration, ShieldStrength strength, string description) => new ItemDefinition
        {
            id = id, itemName = name, artKey = artKey, description = description,
            type = ItemType.Defense, tier = tier, behavior = ItemBehavior.Shield,
            duration = duration, shieldStrength = strength
        };
    }
}
