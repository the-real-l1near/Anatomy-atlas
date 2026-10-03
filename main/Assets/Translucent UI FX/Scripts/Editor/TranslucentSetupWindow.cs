using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TranslucentUIFX.Editor
{
    public enum TranslucentSetupStatus
    {
        Ready,
        NotUsingURP,
        MissingRendererData,
        MissingRendererFeature
    }

    public static class TranslucentSetupUtility
    {
        public static TranslucentSetupStatus GetStatus(Camera camera = null)
        {
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset))
                return TranslucentSetupStatus.NotUsingURP;

            if (!TryGetActiveRendererData(out ScriptableRendererData rendererData, camera))
                return TranslucentSetupStatus.MissingRendererData;

            foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
            {
                if (feature is TranslucentRendererFeature && feature.isActive)
                    return TranslucentSetupStatus.Ready;
            }

            return TranslucentSetupStatus.MissingRendererFeature;
        }

        public static bool TryGetActiveRendererData(out ScriptableRendererData rendererData, Camera camera = null)
        {
            rendererData = null;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urpAsset))
                return false;

            FieldInfo rendererDataListField = typeof(UniversalRenderPipelineAsset).GetField(
                "m_RendererDataList",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (!(rendererDataListField?.GetValue(urpAsset) is ScriptableRendererData[] rendererDataArray) || rendererDataArray.Length == 0)
                return false;

            FieldInfo indexField = typeof(UniversalRenderPipelineAsset).GetField(
                "m_DefaultRendererIndex",
                BindingFlags.Instance | BindingFlags.NonPublic);

            int defaultIndex = indexField != null ? (int)indexField.GetValue(urpAsset) : 0;
            if (camera != null && camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
            {
                using var cameraObject = new SerializedObject(cameraData);
                var rendererIndex = cameraObject.FindProperty("m_RendererIndex");
                if (rendererIndex != null && rendererIndex.intValue >= 0 && rendererIndex.intValue < rendererDataArray.Length)
                    defaultIndex = rendererIndex.intValue;
            }
            defaultIndex = Mathf.Clamp(defaultIndex, 0, rendererDataArray.Length - 1);
            rendererData = rendererDataArray[defaultIndex];
            return rendererData != null;
        }

        public static bool InstallRendererFeature(out string error, Camera camera = null)
        {
            error = null;

            TranslucentSetupStatus status = GetStatus(camera);
            if (status == TranslucentSetupStatus.Ready)
                return true;

            if (!TryGetActiveRendererData(out ScriptableRendererData rendererData, camera))
            {
                error = status == TranslucentSetupStatus.NotUsingURP
                    ? "The active project is not using the Universal Render Pipeline."
                    : "The active URP Renderer Data asset could not be resolved.";
                return false;
            }

            foreach (var existing in rendererData.rendererFeatures)
            {
                if (!(existing is TranslucentRendererFeature)) continue;
                Undo.RecordObject(existing, "Enable Liquid Glass background");
                existing.SetActive(true);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                TranslucentRendererFeature.RequestUpdate();
                return true;
            }

            string rendererPath = AssetDatabase.GetAssetPath(rendererData);
            if (string.IsNullOrEmpty(rendererPath))
            {
                error = "The active Renderer Data is not a saved project asset.";
                return false;
            }

            TranslucentRendererFeature feature = ScriptableObject.CreateInstance<TranslucentRendererFeature>();
            feature.name = "Translucent Renderer Feature";
            int insertedIndex = -1;

            try
            {
                Undo.RegisterCompleteObjectUndo(rendererData, "Install Translucent Renderer Feature");
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localIdentifier) || localIdentifier == 0)
                    throw new InvalidOperationException("Unity could not register the Renderer Feature as a sub-asset.");

                SerializedObject rendererObject = new SerializedObject(rendererData);
                SerializedProperty featureList = rendererObject.FindProperty("m_RendererFeatures");
                SerializedProperty featureMap = rendererObject.FindProperty("m_RendererFeatureMap");

                if (featureList == null || featureMap == null)
                    throw new InvalidOperationException("This URP version does not expose the expected Renderer Feature fields.");

                rendererObject.Update();
                insertedIndex = featureList.arraySize;
                featureList.InsertArrayElementAtIndex(insertedIndex);
                featureList.GetArrayElementAtIndex(insertedIndex).objectReferenceValue = feature;
                featureMap.InsertArrayElementAtIndex(insertedIndex);
                featureMap.GetArrayElementAtIndex(insertedIndex).longValue = localIdentifier;
                rendererObject.ApplyModifiedPropertiesWithoutUndo();

                feature.Create();
                EditorUtility.SetDirty(feature);
                EditorUtility.SetDirty(rendererData);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(rendererPath);
                TranslucentRendererFeature.RequestUpdate();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;

                if (insertedIndex >= 0)
                {
                    SerializedObject rendererObject = new SerializedObject(rendererData);
                    SerializedProperty featureList = rendererObject.FindProperty("m_RendererFeatures");
                    SerializedProperty featureMap = rendererObject.FindProperty("m_RendererFeatureMap");
                    rendererObject.Update();

                    if (featureList != null && insertedIndex < featureList.arraySize)
                    {
                        featureList.GetArrayElementAtIndex(insertedIndex).objectReferenceValue = null;
                        featureList.DeleteArrayElementAtIndex(insertedIndex);
                    }

                    if (featureMap != null && insertedIndex < featureMap.arraySize)
                        featureMap.DeleteArrayElementAtIndex(insertedIndex);

                    rendererObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(rendererData);
                }

                if (feature != null)
                    UnityEngine.Object.DestroyImmediate(feature, true);

                AssetDatabase.SaveAssets();
                return false;
            }
        }

        public static void HighlightRendererData()
        {
            if (!TryGetActiveRendererData(out ScriptableRendererData rendererData)) return;
            Selection.activeObject = rendererData;
            EditorGUIUtility.PingObject(rendererData);
        }
    }

    [InitializeOnLoad]
    public class TranslucentSetupWindow : EditorWindow
    {
        private const string SetupShownSessionKey = "TranslucentUIFX_SetupShown_V2";
        private string m_LastError;

        static TranslucentSetupWindow()
        {
            EditorApplication.delayCall += Initialize;
        }

        private static void Initialize()
        {
            if (Application.isBatchMode) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(SetupShownSessionKey, false)) return;
            if (TranslucentSetupUtility.GetStatus() == TranslucentSetupStatus.Ready) return;
            if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)) return;

            SessionState.SetBool(SetupShownSessionKey, true);
            ShowWindow();
        }

        [MenuItem("Tools/Translucent UI FX/Setup & Diagnostics", false, 0)]
        public static void ShowWindow()
        {
            TranslucentSetupWindow window = GetWindow<TranslucentSetupWindow>(true, "Translucent UI FX Setup", true);
            window.minSize = new Vector2(480, 340);
            window.Show();
        }

        private void OnGUI()
        {
            TranslucentSetupStatus status = TranslucentSetupUtility.GetStatus();

            EditorGUILayout.Space(18);
            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
            EditorGUILayout.LabelField("Translucent UI FX", titleStyle, GUILayout.Height(28));

            GUIStyle subtitleStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 12,
                wordWrap = true
            };
            EditorGUILayout.LabelField("One-time rendering setup and project diagnostics", subtitleStyle, GUILayout.Height(22));
            EditorGUILayout.Space(14);

            DrawStatus(status);
            EditorGUILayout.Space(12);

            using (new EditorGUI.DisabledScope(status != TranslucentSetupStatus.MissingRendererFeature))
            {
                GUIStyle installStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    fixedHeight = 40
                };

                if (GUILayout.Button(status == TranslucentSetupStatus.Ready ? "Renderer Feature Installed" : "Install Renderer Feature", installStyle))
                {
                    m_LastError = null;
                    if (!TranslucentSetupUtility.InstallRendererFeature(out m_LastError))
                        Repaint();
                }
            }

            if (!string.IsNullOrEmpty(m_LastError))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(m_LastError, MessageType.Error);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Active Renderer", GUILayout.Height(28)))
                TranslucentSetupUtility.HighlightRendererData();

            if (GUILayout.Button("Open Manual", GUILayout.Height(28)))
            {
                UnityEngine.Object manual = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Translucent UI FX/Translucent UI FX Docs.pdf");
                if (manual != null)
                {
                    Selection.activeObject = manual;
                    EditorGUIUtility.PingObject(manual);
                    AssetDatabase.OpenAsset(manual);
                }
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Tip: Create a ready-made panel from GameObject > UI > Liquid Glass Panel.", subtitleStyle);
            EditorGUILayout.Space(12);
        }

        private static void DrawStatus(TranslucentSetupStatus status)
        {
            switch (status)
            {
                case TranslucentSetupStatus.Ready:
                    EditorGUILayout.HelpBox("Ready. The active URP Renderer can capture and blur backgrounds for Translucent UI FX.", MessageType.Info);
                    break;
                case TranslucentSetupStatus.NotUsingURP:
                    EditorGUILayout.HelpBox("Universal Render Pipeline is not active. Assign a URP Asset in Project Settings > Graphics before continuing.", MessageType.Error);
                    break;
                case TranslucentSetupStatus.MissingRendererData:
                    EditorGUILayout.HelpBox("The active URP Renderer Data could not be found. Check the renderer list on your URP Asset.", MessageType.Error);
                    break;
                case TranslucentSetupStatus.MissingRendererFeature:
                    EditorGUILayout.HelpBox("Setup needed. Add the Translucent Renderer Feature to the active Renderer Data.", MessageType.Warning);
                    break;
            }
        }
    }
}
