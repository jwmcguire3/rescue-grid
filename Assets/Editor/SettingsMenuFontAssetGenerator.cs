#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class SettingsMenuFontAssetGenerator
{
    private const string SourceRoot = "Assets/Rescue.Unity/Art/UI/Fonts/Source";
    private const string AssetRoot = "Assets/Resources/Rescue.Unity/UI/Fonts";

    [MenuItem("Rescue Grid/UI/Regenerate Settings Menu Fonts")]
    public static void Regenerate()
    {
        Directory.CreateDirectory(AssetRoot);

        TMP_FontAsset robotoSlab = CreateFontAsset(
            $"{SourceRoot}/RobotoSlab-VariableFont_wght.ttf",
            $"{AssetRoot}/Roboto Slab SDF.asset");
        TMP_FontAsset rye = CreateFontAsset(
            $"{SourceRoot}/Rye-Regular.ttf",
            $"{AssetRoot}/Rye SDF.asset");
        TMP_FontAsset dmSans = CreateFontAsset(
            $"{SourceRoot}/DMSans-VariableFont_opsz,wght.ttf",
            $"{AssetRoot}/DM Sans SDF.asset");

        rye.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
        rye.fallbackFontAssetTable.Clear();
        rye.fallbackFontAssetTable.Add(robotoSlab);
        EditorUtility.SetDirty(rye);
        EditorUtility.SetDirty(dmSans);
        EditorUtility.SetDirty(robotoSlab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static TMP_FontAsset CreateFontAsset(string sourcePath, string assetPath)
    {
        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source is null)
        {
            throw new FileNotFoundException($"Could not load source font '{sourcePath}'.");
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            source,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        fontAsset.name = Path.GetFileNameWithoutExtension(assetPath);
        Material material = new Material(Shader.Find("TextMeshPro/Distance Field"))
        {
            name = $"{fontAsset.name} Material"
        };
        Texture2D atlasTexture = new Texture2D(1024, 1024, TextureFormat.Alpha8, false)
        {
            name = $"{fontAsset.name} Atlas"
        };
        atlasTexture.Apply(false, false);
        material.SetTexture("_MainTex", atlasTexture);
        fontAsset.atlasTextures = new[] { atlasTexture };
        fontAsset.material = material;

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) is not null)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        AssetDatabase.CreateAsset(fontAsset, assetPath);
        AssetDatabase.AddObjectToAsset(material, fontAsset);
        AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
        return fontAsset;
    }
}
#endif
