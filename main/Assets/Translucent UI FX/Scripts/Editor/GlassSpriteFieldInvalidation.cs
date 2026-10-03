using System;
using UnityEditor;

namespace TranslucentUIFX.Editor
{
    // Texture imports can keep the same Unity object ID while replacing its alpha.
    internal sealed class GlassSpriteFieldInvalidation : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (string path in imported)
                if (AssetImporter.GetAtPath(path) is TextureImporter || path.EndsWith(".spriteatlas", StringComparison.OrdinalIgnoreCase))
                {
                    GlassSpriteField.InvalidateAll();
                    return;
                }
        }
    }
}
