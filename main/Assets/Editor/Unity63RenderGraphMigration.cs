using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[InitializeOnLoad]
internal static class Unity63RenderGraphMigration
{
    private const string GlobalSettingsPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";
    private const string CompatibilityDefine = "URP_COMPATIBILITY_MODE";

    static Unity63RenderGraphMigration()
    {
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        bool changed = false;

        UnityEngine.Object globalSettings = AssetDatabase.LoadMainAssetAtPath(GlobalSettingsPath);
        if (globalSettings != null)
        {
            var serializedSettings = new SerializedObject(globalSettings);
            SerializedProperty enableRenderGraph = serializedSettings.FindProperty("m_EnableRenderGraph");

            if (enableRenderGraph != null && !enableRenderGraph.boolValue)
            {
                enableRenderGraph.boolValue = true;
                serializedSettings.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(globalSettings);
                changed = true;
            }
        }

        string defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone);
        string cleanedDefines = string.Join(";", defines
            .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(symbol => symbol.Trim())
            .Where(symbol => !string.Equals(symbol, CompatibilityDefine, StringComparison.Ordinal))
            .Distinct());

        if (!string.Equals(defines, cleanedDefines, StringComparison.Ordinal))
        {
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Standalone, cleanedDefines);
            changed = true;
        }

        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[Unity 6.3 Migration] RenderGraph enabled and URP compatibility define removed.");
        }
    }
}
