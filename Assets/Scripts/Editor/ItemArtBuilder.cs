using System.Collections.Generic;
using M2.Items;
using UnityEditor;
using UnityEngine;

namespace M2.Editor
{
    public static class ItemArtBuilder
    {
        const string SpriteRoot = "Assets/Art/Sprites";
        const string LibraryPath = "Assets/Resources/ItemSpriteLibrary.asset";

        static readonly (NetItemId id, string file, Color tint)[] Mappings =
        {
            (NetItemId.Gasoline, "Gasoline.png", Color.white),
            (NetItemId.SuperGasoline, "Super Gasoline.png", Color.white),
            (NetItemId.HappyBirthdayToYou, "HBD Gasoline.png", Color.white),
            (NetItemId.JaeSeokGasoline, "Jae-seok Gasoline.png", Color.white),
            (NetItemId.Bomb, "bomb.png", Color.white),
            (NetItemId.C4, "c4_bomb.png", Color.white),
            (NetItemId.Dynamite, "dynamite.png", Color.white),
            (NetItemId.StickGrenade, "stick_grenade.png", Color.white),
            (NetItemId.AtomicBomb, "atomic_bomb.png", Color.white),
            (NetItemId.LoveLetter, "myeongjin_love_letter.png", Color.white),
            (NetItemId.Shield, "shield.png", Color.white),
            (NetItemId.SpikedShield, "spike_shield.png", Color.white),
            // Golden shield deliberately reuses the original wooden shield with a gold tint.
            // jindungongcheong.png is a real-world state emblem and is forbidden by CLAUDE.md.
            (NetItemId.GoldenShield, "shield.png", new Color(1f, 0.72f, 0.12f, 1f)),
        };

        [MenuItem("M2/Build Item Sprite Library")]
        public static void Build()
        {
            EnsureFolder("Assets/Resources");
            var entries = new List<ItemSpriteEntry>(Mappings.Length);

            foreach ((NetItemId id, string file, Color tint) in Mappings)
            {
                string path = $"{SpriteRoot}/{file}";
                ConfigureAsSprite(path);
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) throw new System.InvalidOperationException($"Item sprite missing: {path}");
                entries.Add(new ItemSpriteEntry { id = id, sprite = sprite, tint = tint });
            }

            ItemSpriteLibrary library = AssetDatabase.LoadAssetAtPath<ItemSpriteLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<ItemSpriteLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.ReplaceEntries(entries);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"M2_ITEM_ART_LIBRARY_OK: {entries.Count} entries");
        }

        static void ConfigureAsSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new System.InvalidOperationException($"Texture importer missing: {path}");
            if (importer.textureType == TextureImporterType.Sprite && !importer.mipmapEnabled &&
                Mathf.Approximately(importer.spritePixelsPerUnit, 512f)) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 512f;
            importer.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
    }
}
