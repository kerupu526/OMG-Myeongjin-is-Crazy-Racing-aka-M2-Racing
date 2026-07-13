using System.Collections;
using M2.Core;
using M2.Player;
using UnityEngine;

namespace M2.Items
{
    public static class ItemEffects
    {
        public static RemoteC4Charge SpawnAttack(Vector3 position, ItemDefinition definition,
            VehicleController owner)
        {
            GameObject marker = SpawnBombVisual(position, definition);
            switch (definition.behavior)
            {
                case ItemBehavior.RemoteC4:
                    RemoteC4Charge charge = marker.AddComponent<RemoteC4Charge>();
                    charge.Init(definition, owner);
                    return charge;
                case ItemBehavior.ProximityGrenade:
                    marker.AddComponent<ProximityGrenadeRunner>().Init(definition, owner);
                    break;
                case ItemBehavior.AtomicBomb:
                    marker.AddComponent<ImmediateAttackRunner>().Init(definition, owner);
                    break;
                default:
                    marker.AddComponent<TimedAttackRunner>().Init(definition, owner);
                    break;
            }
            return null;
        }

        // Backward-compatible entry used by older tests/callers.
        public static void SpawnBomb(Vector3 position, ItemDefinition definition) =>
            SpawnAttack(position, definition, null);

        public static GameObject SpawnBombVisual(Vector3 position, ItemDefinition definition)
        {
            GameObject marker = new GameObject($"Bomb_{definition.itemName}");
            marker.transform.position = position;

            GameObject spriteChild = new GameObject("Sprite");
            spriteChild.transform.SetParent(marker.transform);
            spriteChild.transform.localPosition = Vector3.up * 0.4f;
            SpriteRenderer spriteRenderer = spriteChild.AddComponent<SpriteRenderer>();
            if (ItemArt.TryGet(definition.id, out Sprite sprite, out Color tint))
            {
                spriteRenderer.sprite = sprite;
                spriteRenderer.color = tint;
            }
            else
            {
                spriteRenderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite(
                    Color.black, new Color(1f, 0.3f, 0f), 64, 64f);
            }
            spriteRenderer.sortingOrder = 5;
            spriteChild.AddComponent<BillboardSprite>();
            return marker;
        }

        public static void ResolveAttack(Vector3 position, ItemDefinition definition,
            VehicleController owner)
        {
            var resolved = new System.Collections.Generic.HashSet<VehicleController>();
            foreach (Collider hit in Physics.OverlapSphere(position, definition.attackRadius))
            {
                if (!hit.CompareTag("Player")) continue;
                VehicleController vehicle = hit.GetComponentInParent<VehicleController>();
                if (vehicle == null || !resolved.Add(vehicle)) continue;

                if (vehicle.TryBlockAttack(definition, out bool reflected))
                {
                    if (reflected && owner != null && owner != vehicle) owner.ApplyHitStun();
                    continue;
                }
                vehicle.ApplyHitStun(definition.behavior == ItemBehavior.AtomicBomb ? 3f : 0.6f);
            }

            if (definition.heartEffect) SpawnHeartEffect(position);
            if (definition.behavior == ItemBehavior.AtomicBomb)
            {
                Object.FindFirstObjectByType<GameManager>()?.EndRaceAsDraw("원자폭탄");
            }
        }

        public static void SpawnHeartEffect(Vector3 position)
        {
            for (int i = 0; i < 5; i++)
            {
                GameObject heart = new GameObject("LoveLetterHeart");
                heart.transform.position = position + Vector3.up * (0.5f + i * 0.2f);
                SpriteRenderer renderer = heart.AddComponent<SpriteRenderer>();
                renderer.sprite = PlaceholderSpriteFactory.CreateCircleSprite(
                    new Color(1f, 0.25f, 0.55f), Color.white, 32, 32f);
                renderer.sortingOrder = 8;
                heart.AddComponent<BillboardSprite>();
                Object.Destroy(heart, 1.2f);
            }
        }
    }

    public abstract class AttackRunner : MonoBehaviour
    {
        protected ItemDefinition definition;
        protected VehicleController owner;

        public virtual void Init(ItemDefinition item, VehicleController source)
        {
            definition = item;
            owner = source;
        }

        protected void Explode()
        {
            ItemEffects.ResolveAttack(transform.position, definition, owner);
            Destroy(gameObject);
        }
    }

    public class TimedAttackRunner : AttackRunner
    {
        public override void Init(ItemDefinition item, VehicleController source)
        {
            base.Init(item, source);
            StartCoroutine(ArmThenExplode());
        }

        IEnumerator ArmThenExplode()
        {
            yield return new WaitForSeconds(definition.armTime);
            Explode();
        }
    }

    public class ImmediateAttackRunner : AttackRunner
    {
        public override void Init(ItemDefinition item, VehicleController source)
        {
            base.Init(item, source);
            Explode();
        }
    }

    public class RemoteC4Charge : AttackRunner
    {
        public void Detonate() => Explode();
    }

    public class ProximityGrenadeRunner : AttackRunner
    {
        public override void Init(ItemDefinition item, VehicleController source)
        {
            base.Init(item, source);
            StartCoroutine(WaitForOpponent());
        }

        IEnumerator WaitForOpponent()
        {
            while (true)
            {
                foreach (Collider hit in Physics.OverlapSphere(transform.position, definition.triggerDistance))
                {
                    VehicleController vehicle = hit.GetComponentInParent<VehicleController>();
                    if (vehicle != null && vehicle != owner)
                    {
                        Explode();
                        yield break;
                    }
                }
                yield return new WaitForFixedUpdate();
            }
        }
    }
}
