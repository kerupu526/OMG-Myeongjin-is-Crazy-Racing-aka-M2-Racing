using System.Collections;
using M2.Items;
using UnityEngine;
using UnityEngine.UI;

namespace M2.UI
{
    // Brief on-screen text confirming an item was used (e.g. "휘발유 사용!"). Without this
    // the accel boost in particular is basically invisible — no VFX to notice it yet.
    public class ItemUseNotifier : MonoBehaviour
    {
        ItemSlots itemSlots;
        public Text label;
        public float displaySeconds = 1.5f;

        Coroutine hideRoutine;

        // Explicit bind instead of wiring `itemSlots` as a public field: AddComponent<T>()
        // runs OnEnable synchronously, before a caller gets the chance to assign fields on
        // the returned instance — subscribing there silently no-ops on a still-null slots ref.
        public void Bind(ItemSlots slots)
        {
            if (itemSlots != null) itemSlots.OnItemUsed -= HandleItemUsed;
            itemSlots = slots;
            if (itemSlots != null) itemSlots.OnItemUsed += HandleItemUsed;
        }

        void OnEnable()
        {
            if (label != null) label.text = "";
        }

        void OnDisable()
        {
            if (itemSlots != null) itemSlots.OnItemUsed -= HandleItemUsed;
        }

        void HandleItemUsed(ItemDefinition definition)
        {
            if (label == null) return;

            label.text = $"{definition.itemName} 사용!";
            if (hideRoutine != null) StopCoroutine(hideRoutine);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        IEnumerator HideAfterDelay()
        {
            yield return new WaitForSeconds(displaySeconds);
            label.text = "";
            hideRoutine = null;
        }
    }
}
