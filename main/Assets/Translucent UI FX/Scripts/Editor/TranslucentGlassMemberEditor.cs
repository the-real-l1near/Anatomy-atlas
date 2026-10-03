using UnityEditor;
using UnityEngine;

namespace TranslucentUIFX.Editor
{
    [CustomEditor(typeof(TranslucentGlassMember)), CanEditMultipleObjects]
    public sealed class TranslucentGlassMemberEditor : UnityEditor.Editor
    {
        private bool m_ShowAppearance;
        private void OnEnable() { m_ShowAppearance = SessionState.GetBool("LiquidGlass.Inspector.MemberAppearance", false); }
        private SerializedProperty Property(string name) => serializedObject.FindProperty(name);
        public override void OnInspectorGUI()
        {
            using var layout = new GlassInspectorGUI.Layout();
            serializedObject.Update();
            var member = (TranslucentGlassMember)target;
            GlassInspectorGUI.Header("Glass Shape", "One shape. Part of something larger.", member.Operation == GlassOperation.Cutout ? "CUTOUT" : "MEMBER");
            if (member.Owner == null) EditorGUILayout.HelpBox("Place this shape below a Liquid Glass Fusion group.", MessageType.Warning);
            else using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.ObjectField("Surface", member.Owner, typeof(TranslucentGlassGroup), true);
                if (GUILayout.Button("Edit glass", GUILayout.Width(75f))) Selection.activeGameObject = member.Owner.gameObject;
            }
            using (new GlassInspectorGUI.Card())
            {
                GlassInspectorGUI.Title("Composition");
                GlassInspectorGUI.Toggle(Property("m_Include"), "Include shape");
                var operation = Property("m_Operation");
                Rect r = EditorGUILayout.GetControlRect(false, 27f);
                EditorGUI.BeginProperty(r, GUIContent.none, operation);
                EditorGUI.BeginChangeCheck();
                int selected = GUILayoutOperation(r, operation);
                if (EditorGUI.EndChangeCheck()) operation.intValue = selected;
                EditorGUI.EndProperty();
                GUILayout.Label(operation.intValue == 0 ? "Adds glass to the surface and blends with nearby shapes." : "Cuts through the combined surface, including pointer hit areas.", EditorStyles.wordWrappedMiniLabel);
            }
            using (new GlassInspectorGUI.Card())
            {
                GlassInspectorGUI.Title("Shape");
                if (Property("m_ShapeSprite").objectReferenceValue != null || Property("m_ShapeSprite").hasMultipleDifferentValues) GlassInspectorGUI.SpriteSource(Property("m_ShapeSprite"));
                else EditorGUILayout.PropertyField(Property("m_ShapeSprite"), new GUIContent("Shape sprite"));
                if (Property("m_ShapeSprite").objectReferenceValue != null)
                {
                    GlassInspectorGUI.Toggle(Property("m_PreserveAspect"), "Preserve aspect");
                    GlassInspectorGUI.Slider(Property("m_AlphaThreshold"), "Alpha threshold", 0.01f, 0.99f);
                }
                else GlassInspectorGUI.Shape(Property("m_Shape"), Property("m_CornerRadius"), Property("m_CornerContinuity"), Property("m_IndividualCorners"), Property("m_CornerRadii"), Property("CornerSettings"));
                EditorGUILayout.Space(3f);
                GlassInspectorGUI.Slider(Property("m_SurfaceExpansion"), "Local expansion", -32f, 64f, "Fine adjustment in Canvas units.");
            }
            m_ShowAppearance = GlassInspectorGUI.Foldout(m_ShowAppearance, "Appearance", "Inherit or customize");
            if (m_ShowAppearance)
                using (new GlassInspectorGUI.Card())
                {
                    GlassInspectorGUI.Toggle(Property("Appearance.Override"), "Custom appearance");
                    if (Property("Appearance.Override").boolValue)
                    {
                        EditorGUILayout.PropertyField(Property("Appearance.Tint"), new GUIContent("Tint"));
                        EditorGUILayout.PropertyField(Property("Appearance.GraphicColor"), new GUIContent("Graphic color"));
                        GlassInspectorGUI.Slider(Property("Appearance.Blur"), "Blur", 0f, 1f);
                        GlassInspectorGUI.Slider(Property("Appearance.Refraction"), "Refraction", 0f, 0.2f);
                        GlassInspectorGUI.Slider(Property("Appearance.Dispersion"), "Dispersion", 0f, 0.02f);
                        GlassInspectorGUI.Slider(Property("Appearance.Opacity"), "Opacity", 0f, 1f);
                        GlassInspectorGUI.Slider(Property("Appearance.Surface.LensDepth"), "Lens depth", 0f, 20f);
                        GlassInspectorGUI.Slider(Property("Appearance.RefractiveIndex"), "Refractive index", 1f, 2.5f);
                        GlassInspectorGUI.Slider(Property("Appearance.Surface.Magnification"), "Magnification", 1f, 4f);
                        GlassInspectorGUI.Slider(Property("Appearance.Surface.Transmission"), "Transmission", 0f, 2f);
                        GlassInspectorGUI.Slider(Property("Appearance.Surface.OpticalLip"), "Lip width", 0f, 100f);
                        GlassInspectorGUI.Popup(Property("Appearance.Surface.LipUnits"), "Lip units", new[] { "Canvas units", "% of shorter side", "% of longer side" });
                        GlassInspectorGUI.Slider(Property("Appearance.Surface.Smoothness"), "Surface smoothing", 0.01f, 5f);
                        GlassInspectorGUI.Toggle(Property("Appearance.Surface.StabilizeInterior"), "Keep interior calm");
                        GlassInspectorGUI.Popup(Property("Appearance.Surface.BandUnits"), "Band units", new[] { "Fraction of optical lip", "Canvas units" });
                        float bandMax = Property("Appearance.Surface.BandUnits").intValue == 0 ? 1f : 64f;
                        GlassInspectorGUI.Slider(Property("Appearance.Surface.InnerBand"), "Inner band", 0f, bandMax);
                        GlassInspectorGUI.Slider(Property("Appearance.Surface.OuterBand"), "Outer band", 0f, bandMax);
                        GlassInspectorGUI.Slider(Property("Appearance.Highlights"), "Highlights", 0f, 1f);
                        GlassInspectorGUI.Slider(Property("Appearance.EdgeDepth"), "Edge depth", 0f, 1f);
                    }
                    else GUILayout.Label("Uses the group's appearance. Custom values blend through joins.", EditorStyles.wordWrappedMiniLabel);
                }
            SessionState.SetBool("LiquidGlass.Inspector.MemberAppearance", m_ShowAppearance);
            if (serializedObject.ApplyModifiedProperties()) member.Owner?.RefreshMembers();
            EditorGUILayout.Space(4f);
            GUILayout.Label("Move, rotate, resize, or duplicate this object normally.", EditorStyles.wordWrappedMiniLabel);
            using (new EditorGUI.DisabledScope(member.Owner == null))
                if (GlassInspectorGUI.Action("Detach as standalone glass"))
                {
                    var members = System.Array.ConvertAll(targets, item => (TranslucentGlassMember)item);
                    if (TranslucentFusionAuthoring.TryDetach(members, out _, out string error)) GUIUtility.ExitGUI();
                    else EditorUtility.DisplayDialog("Cannot detach glass", error, "OK");
                }
        }
        private static int GUILayoutOperation(Rect rect, SerializedProperty property)
        {
            return GUI.Toolbar(rect, property.hasMultipleDifferentValues ? -1 : property.intValue, new[] { "Add glass", "Cut out" });
        }
    }
}
