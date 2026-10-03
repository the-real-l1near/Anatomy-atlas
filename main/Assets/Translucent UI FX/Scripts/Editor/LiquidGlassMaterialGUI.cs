using UnityEditor;
using UnityEngine;

namespace TranslucentUIFX.Editor
{
    public sealed class LiquidGlassMaterialGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            GUILayout.Label("Managed by Liquid Glass", EditorStyles.boldLabel);
            GUILayout.Label("Edit Appearance, Shape, and Fusion on the UI component. Its material is maintained automatically.", EditorStyles.wordWrappedMiniLabel);
        }
    }
}
