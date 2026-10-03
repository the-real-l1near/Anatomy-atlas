using UnityEditor;
using UnityEngine;

namespace TranslucentUIFX.Editor
{
    [CustomEditor(typeof(TranslucentRendererFeature)), CanEditMultipleObjects]
    public sealed class TranslucentRendererFeatureEditor : UnityEditor.Editor
    {
        private bool advanced;
        private SerializedProperty P(string name) => serializedObject.FindProperty("settings." + name);
        public override void OnInspectorGUI()
        {
            using var layout = new GlassInspectorGUI.Layout();
            serializedObject.Update();
            GlassInspectorGUI.Header("Glass Capture", "Shared background rendering for Liquid Glass.", "URP");
            using (new GlassInspectorGUI.Card())
            {
                Rect row = EditorGUILayout.GetControlRect(false, 24);
                var stage = P("injectionPoint");
                EditorGUI.BeginProperty(row, new GUIContent("Capture point"), stage);
                EditorGUI.BeginChangeCheck();
                int selected = EditorGUI.Popup(row, "Capture point", stage.hasMultipleDifferentValues ? -1 : stage.enumValueIndex,
                    new[] { "Automatic", "Before transparents", "After transparents", "After post-processing" });
                if (EditorGUI.EndChangeCheck()) stage.enumValueIndex = selected;
                EditorGUI.EndProperty();
                EditorGUILayout.PropertyField(P("captureLayer"), new GUIContent("Layer"));
                GUILayout.Label("Use a different layer number for each capture when stacking glass. Match it on a component or shared Capture Source.", EditorStyles.wordWrappedMiniLabel);
            }
            advanced = GlassInspectorGUI.Foldout(advanced, "Quality tuning", "Optional");
            if (advanced)
                using (new GlassInspectorGUI.Card())
                {
                    GlassInspectorGUI.Slider(P("backdropMipLevels"), "Backdrop mips", 0, 8);
                    GlassInspectorGUI.Slider(P("blurPasses"), "Blur passes", 0, 6);
                    GlassInspectorGUI.Slider(P("kernelSpread"), "Kernel spread", 0.1f, 4f);
                    GlassInspectorGUI.Slider(P("dither"), "Dither", 0f, 2f);
                    GlassInspectorGUI.Toggle(P("matchCameraFormat"), "Match camera format");
                    if (!P("matchCameraFormat").boolValue) EditorGUILayout.PropertyField(P("captureFormat"), new GUIContent("Capture format"));
                    GUILayout.Label("Component capture quality and blur requests determine shared work. A camera override can tune one camera independently.", EditorStyles.wordWrappedMiniLabel);
                }
            if (serializedObject.ApplyModifiedProperties()) TranslucentRendererFeature.RequestUpdate();
        }
    }
}
