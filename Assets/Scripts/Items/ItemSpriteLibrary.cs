using System;
using System.Collections.Generic;
using UnityEngine;

namespace M2.Items
{
    [Serializable]
    public class ItemSpriteEntry
    {
        public NetItemId id;
        public Sprite sprite;
        public Color tint = Color.white;
    }

    public class ItemSpriteLibrary : ScriptableObject
    {
        [SerializeField] List<ItemSpriteEntry> entries = new List<ItemSpriteEntry>();

        public IReadOnlyList<ItemSpriteEntry> Entries => entries;

        public void ReplaceEntries(IEnumerable<ItemSpriteEntry> replacements)
        {
            entries.Clear();
            entries.AddRange(replacements);
        }

        public bool TryGet(NetItemId id, out Sprite sprite, out Color tint)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].id != id) continue;
                sprite = entries[i].sprite;
                tint = entries[i].tint;
                return sprite != null;
            }

            sprite = null;
            tint = Color.white;
            return false;
        }
    }

    public static class ItemArt
    {
        static ItemSpriteLibrary library;

        public static bool TryGet(NetItemId id, out Sprite sprite, out Color tint)
        {
            if (library == null) library = Resources.Load<ItemSpriteLibrary>("ItemSpriteLibrary");
            if (library != null && library.TryGet(id, out sprite, out tint)) return true;

            sprite = null;
            tint = Color.white;
            return false;
        }
    }
}
