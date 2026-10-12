using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class BuildVietnameseFontAssets
{
    [MenuItem("Tools/Localization/Create Be Vietnam Pro Font Assets")]
    public static void CreateAssets()
    {
        Create("Assets/Fonts/BeVietnamPro/BeVietnamPro-Regular.ttf", "Assets/Fonts/BeVietnamPro/BeVietnamPro-Regular SDF.asset");
        Create("Assets/Fonts/BeVietnamPro/BeVietnamPro-SemiBold.ttf", "Assets/Fonts/BeVietnamPro/BeVietnamPro-SemiBold SDF.asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void Create(string sourcePath, string outputPath)
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
            throw new System.InvalidOperationException("Could not load font: " + sourcePath);

        var asset = TMP_FontAsset.CreateFontAsset(
            source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
            AtlasPopulationMode.Dynamic, true);
        if (asset == null)
            throw new System.InvalidOperationException("Could not create TMP font asset: " + sourcePath);

        AssetDatabase.CreateAsset(asset, outputPath);
        var material = asset.material;
        if (material != null && AssetDatabase.GetAssetPath(material) == string.Empty)
            AssetDatabase.AddObjectToAsset(material, asset);
        foreach (var texture in asset.atlasTextures)
            if (texture != null && AssetDatabase.GetAssetPath(texture) == string.Empty)
                AssetDatabase.AddObjectToAsset(texture, asset);
    }
}
