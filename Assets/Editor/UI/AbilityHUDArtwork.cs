using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Creates the initial pixel sprites once; subsequent layout updates preserve edited artwork.
public static class AbilityHUDArtwork
{
    private const string ArtRoot = "Assets/Graphics/UI/Abilities";

    public static Sprite DialBody() => Sprite("DialBody", 32, (x, y) =>
    {
        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16));
        bool lit = y > x;
        if (distance >= 16) return Color.clear;
        if (distance >= 15) return Hex(0x151410);
        if (distance >= 14) return Hex(lit ? 0xb29361u : 0x5c4630u);
        if (distance >= 13.5f) return Hex(0x3a3024);
        if (distance >= 11.5f) return Hex(0x27231c);
        if (distance >= 10.5f) return Hex(lit ? 0x62503au : 0x30291fu);
        return Hex(y > 16 ? 0x282720u : 0x1d1d19u);
    });

    public static Sprite DialRing() => Sprite("DialRing", 32, (x, y) =>
    {
        float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16, 16));
        return distance >= 11.5f && distance < 13.5f ? Color.white : Color.clear;
    });

    public static Sprite GroundSlam() => AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/SkillA_icon_v2.png");

    public static Sprite Counter() => AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/SkillS_icon_v2.png");

    private static Color Hex(uint value) => new Color32(
        (byte)(value >> 16), (byte)(value >> 8), (byte)value, 255);

    private static Sprite Sprite(string name, int size, Func<int, int, Color> pixel)
    {
        string path = ArtRoot + "/" + name + ".png";
        if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Sprite>(path);

        Directory.CreateDirectory(ArtRoot);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        try
        {
            var colors = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    colors[y * size + x] = pixel(x, y);
            texture.SetPixels(colors);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
