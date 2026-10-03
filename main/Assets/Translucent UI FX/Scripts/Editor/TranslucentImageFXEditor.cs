using UnityEditor;
using UnityEditor.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TranslucentUIFX.Editor
{
    [CustomEditor(typeof(TranslucentImageFX), true)]
    [CanEditMultipleObjects]
    public class TranslucentImageFXEditor : ImageEditor
    {
        private bool m_ShowAdvanced;
        private bool m_ShowCapture;
        private bool m_ShowBackgroundOverride;
        private bool m_ShowLighting;
        private bool m_ShowImageSettings;
        private readonly GlassFusionDiagram m_CompositionMap = new GlassFusionDiagram();
        protected override void OnEnable()
        {
            base.OnEnable();
            m_ShowAdvanced = SessionState.GetBool("LiquidGlass.Inspector.Optics", false);
            m_ShowCapture = SessionState.GetBool("LiquidGlass.Inspector.Capture", false);
            m_ShowLighting = SessionState.GetBool("LiquidGlass.Inspector.Lighting", false);
            m_ShowImageSettings = SessionState.GetBool("LiquidGlass.Inspector.Interaction", false);
        }
        protected override void OnDisable() { m_CompositionMap.Dispose(); base.OnDisable(); }
        private SerializedProperty Property(string name) => serializedObject.FindProperty(name);

        [MenuItem("GameObject/UI/Liquid Glass Panel", false, 1)]
        private static void CreateLiquidGlassPanel(MenuCommand menuCommand)
        {
            CreateTranslucentElement(menuCommand, "Liquid Glass Panel", new Vector2(320f, 160f));
        }

        [MenuItem("GameObject/UI/Fused Liquid Glass Group", false, 2)]
        private static void CreateFusedGlassGroup(MenuCommand menuCommand)
        {
            GameObject parent = ResolveCanvasParent(menuCommand);
            GameObject groupObject = new GameObject("Fused Liquid Glass Group");
            Undo.RegisterCreatedObjectUndo(groupObject, "Create Fused Liquid Glass Group");
            RectTransform groupRect = groupObject.AddComponent<RectTransform>();
            groupObject.AddComponent<CanvasRenderer>();
            TranslucentGlassGroup group = groupObject.AddComponent<TranslucentGlassGroup>();
            groupRect.sizeDelta = new Vector2(520f, 260f);
            group.ApplyLiquidGlass();
            group.raycastTarget = false;
            GameObjectUtility.SetParentAndAlign(groupObject, parent);

            CreateDefaultMember(groupObject.transform, "Glass Member A", new Vector2(-95f, 25f), new Vector2(280f, 132f));
            CreateDefaultMember(groupObject.transform, "Glass Member B", new Vector2(105f, -18f), new Vector2(250f, 132f));
            group.RefreshMembers();
            Selection.activeGameObject = groupObject;
        }

        private static void CreateTranslucentElement(MenuCommand menuCommand, string objectName, Vector2 size)
        {
            GameObject parent = ResolveCanvasParent(menuCommand);
            GameObject gameObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create " + objectName);
            RectTransform rect = gameObject.AddComponent<RectTransform>();
            gameObject.AddComponent<CanvasRenderer>();
            TranslucentImageFX effect = gameObject.AddComponent<TranslucentImageFX>();

            rect.sizeDelta = size;
            effect.ApplyLiquidGlass();
            GameObjectUtility.SetParentAndAlign(gameObject, parent);
            Selection.activeGameObject = gameObject;
        }

        private static GameObject ResolveCanvasParent(MenuCommand menuCommand)
        {
            GameObject parent = menuCommand.context as GameObject;
            if (parent == null || parent.GetComponentInParent<Canvas>() == null)
            {
                Canvas canvas = Object.FindAnyObjectByType<Canvas>();
                if (canvas != null)
                {
                    parent = canvas.gameObject;
                }
                else
                {
                    EditorApplication.ExecuteMenuItem("GameObject/UI/Canvas");
                    parent = Selection.activeGameObject;
                }
            }

            return parent;
        }

        private static void CreateDefaultMember(Transform parent, string objectName, Vector2 position, Vector2 size)
        {
            GameObject memberObject = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(memberObject, "Create Glass Member");
            RectTransform rect = memberObject.AddComponent<RectTransform>();
            memberObject.AddComponent<CanvasRenderer>();
            Image interactionSurface = memberObject.AddComponent<Image>();
            interactionSurface.color = Color.clear;
            memberObject.AddComponent<TranslucentGlassMember>();
            rect.SetParent(parent, false);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public override void OnInspectorGUI()
        {
            using var layout = new GlassInspectorGUI.Layout();
            serializedObject.Update();
            var selectedGlass = (TranslucentImageFX)target;
            TranslucentSetupStatus status = TranslucentSetupUtility.GetStatus(selectedGlass.ResolvedCaptureCamera);
            bool grouped = target is TranslucentGlassGroup;
            GlassInspectorGUI.Header(grouped ? "Liquid Glass Fusion" : "Liquid Glass", "Shape light, simply.", status != TranslucentSetupStatus.Ready ? "SETUP" : selectedGlass.HasBackgroundImage ? "READY" : "WAITING");
            if (status != TranslucentSetupStatus.Ready)
            {
                EditorGUILayout.HelpBox(status == TranslucentSetupStatus.NotUsingURP ? "Liquid Glass requires Universal Render Pipeline." : "Add Translucent Renderer Feature to your active URP renderer.", MessageType.Warning);
                if (GUILayout.Button("Set up background") && !TranslucentSetupUtility.InstallRendererFeature(out var error, selectedGlass.ResolvedCaptureCamera))
                    EditorUtility.DisplayDialog("Liquid Glass", error, "OK");
            }
            using (new GlassInspectorGUI.Card())
            {
                GlassInspectorGUI.Title("Appearance", "A clear center. A little light at the edges.");
                // Color has no runtime decorators; sliders use property-aware controls below.
                EditorGUILayout.PropertyField(Property("TintColor"), new GUIContent("Tint", "Use a low alpha for clear glass."));
                GlassInspectorGUI.FeaturedSlider(Property("BlurStrength"), "Blur", 0f, 1f, "Clear", "Soft");
                GlassInspectorGUI.FeaturedSlider(Property("RefractionAmount"), "Refraction", 0f, 0.12f, "Still", "Sculpted");
                if (!Property("RefractionAmount").hasMultipleDifferentValues && Mathf.Approximately(Property("RefractionAmount").floatValue, 0f))
                    GUILayout.Label("Bending is off. Raise Refraction to reveal the optical edge.", EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.Space(3f);
                GlassInspectorGUI.TogglePair(Property("EnableSpecularGlare"), "Highlights", Property("InteractiveGlare"), "Follow pointer");
                if (Property("InteractiveGlare").boolValue && target is TranslucentImageFX effect && !effect.raycastTarget)
                    EditorGUILayout.HelpBox("Enable Raycast Target in Image & Interaction to use pointer lighting.", MessageType.Info);
            }
            EditorGUILayout.Space(4f);
            if (!grouped)
            {
                using (new GlassInspectorGUI.Card())
                {
                    GlassInspectorGUI.Title("Shape");
                    if (Property("m_Sprite").objectReferenceValue != null || Property("m_Sprite").hasMultipleDifferentValues) GlassInspectorGUI.SpriteSource(Property("m_Sprite"));
                    else EditorGUILayout.PropertyField(Property("m_Sprite"), new GUIContent("Shape sprite", "Optional. Its alpha defines both the outline and the optical edge."));
                    if (Property("m_Sprite").objectReferenceValue != null || Property("m_Sprite").hasMultipleDifferentValues)
                    {
                        GlassInspectorGUI.Slider(Property("SpriteAlphaThreshold"), "Alpha threshold", 0.01f, 0.99f);
                        EditorGUILayout.HelpBox("The sprite's alpha shapes the glass and its refraction. Texture Read/Write is not required.", MessageType.None);
                        if (GlassInspectorGUI.Action("Use procedural shape"))
                        {
                            Property("m_Sprite").objectReferenceValue = null;
                            Property("ProceduralShape").boolValue = true;
                        }
                    }
                    else
                    {
                        Property("ProceduralShape").boolValue = true;
                        GlassInspectorGUI.Shape(Property("EdgeShape"), Property("EdgeRounding"), Property("CornerContinuity"), Property("IndividualCorners"), Property("CornerRadii"), Property("CornerSettings"));
                    }
                }
                DrawFusionActions();
            }
            else DrawGroup((TranslucentGlassGroup)target);

            EditorGUILayout.Space(4f);
            GlassInspectorGUI.Divider("Fine tuning");
            m_ShowAdvanced = GlassInspectorGUI.Foldout(m_ShowAdvanced, "Optics", "Lens & transparency");
            if (m_ShowAdvanced)
                using (new GlassInspectorGUI.Card())
                {
                    GlassInspectorGUI.Title("Surface", "Fine-tune the edge without changing the silhouette.");
                    GlassInspectorGUI.Slider(Property("GlassIntensity"), "Opacity / fade", 0f, 1f);
                    GlassInspectorGUI.Slider(Property("RimDepth"), "Edge depth", 0f, 1f);
                    GlassInspectorGUI.Slider(Property("ChromaticAberration"), "Dispersion", 0f, 0.02f, "A subtle separation of color around the refractive edge.");
                    GlassInspectorGUI.Slider(Property("Surface.Magnification"), "Magnification", 1f, 4f);
                    GlassInspectorGUI.Slider(Property("Surface.Transmission"), "Transmission", 0f, 2f);
                    GlassInspectorGUI.Slider(Property("Surface.LensDepth"), "Lens depth", 0f, 20f);
                    GlassInspectorGUI.Slider(Property("RefractiveIndex"), "Refractive index", 1f, 2.5f);
                    EditorGUILayout.Space(8f);
                    GlassInspectorGUI.Title("Optical edge");
                    GlassInspectorGUI.Popup(Property("Surface.LipUnits"), "Lip units", new[] { "Canvas units", "% of shorter side", "% of longer side" });
                    GlassInspectorGUI.Slider(Property("Surface.OpticalLip"), "Lip width", 0f, Property("Surface.LipUnits").intValue == 0 ? 100f : 50f);
                    GlassInspectorGUI.Slider(Property("Surface.Smoothness"), "Surface smoothing", 0.01f, 5f);
                    GlassInspectorGUI.Toggle(Property("Surface.StabilizeInterior"), "Stabilize interior");
                }
            m_ShowLighting = GlassInspectorGUI.Foldout(m_ShowLighting, "Lighting", "Direction, bands & shadow");
            if (m_ShowLighting)
                using (new GlassInspectorGUI.Card())
                {
                    GlassInspectorGUI.Popup(Property("Surface.LightMode"), "Light", new[] { "Directional", "Opposing", "Point" });
                    if (Property("Surface.LightMode").intValue == 2)
                    {
                        EditorGUILayout.PropertyField(Property("Surface.PointPosition"), new GUIContent("Position"));
                        GlassInspectorGUI.Slider(Property("Surface.PointRadius"), "Reach", 0.01f, 2f);
                    }
                    else GlassInspectorGUI.LightDirection(Property("LightDirection"));
                    if (Property("Surface.LightMode").intValue == 1)
                        GlassInspectorGUI.Slider(Property("Surface.OpposingStrength"), "Opposing light", 0f, 1f);
                    GlassInspectorGUI.Slider(Property("Surface.LightSpread"), "Spread", 0f, 1f);
                    EditorGUILayout.PropertyField(Property("Surface.LightColor"), new GUIContent("Lip highlight"));
                    EditorGUILayout.PropertyField(Property("Surface.LipShadow"), new GUIContent("Lip shadow"));
                    GlassInspectorGUI.Popup(Property("Surface.BandUnits"), "Band units", new[] { "Fraction of optical lip", "Canvas units" });
                    float bandMaximum = Property("Surface.BandUnits").intValue == 0 ? 1f : 64f;
                    GlassInspectorGUI.Slider(Property("Surface.OuterBand"), "Outer band", 0f, bandMaximum);
                    GlassInspectorGUI.Slider(Property("Surface.InnerBand"), "Inner band", 0f, bandMaximum);
                    using (new EditorGUI.DisabledScope(!Property("EnableSpecularGlare").boolValue && !Property("EnableSpecularGlare").hasMultipleDifferentValues))
                        GlassInspectorGUI.Slider(Property("SpecularGlare"), "Highlight strength", 0f, 1f);
                    EditorGUILayout.Space(8f);
                    GlassInspectorGUI.Title("Drop shadow", "Color alpha controls visibility. Zero keeps shadows off.");
                    EditorGUILayout.PropertyField(Property("Surface.ShadowColor"), new GUIContent("Shadow color"));
                    if (Property("Surface.ShadowColor").colorValue.a > 0f || Property("Surface.ShadowColor").hasMultipleDifferentValues)
                    {
                        GlassInspectorGUI.Slider(Property("Surface.ShadowSize"), "Softness", 0f, 32f);
                        EditorGUILayout.PropertyField(Property("Surface.ShadowOffset"), new GUIContent("Offset (pixels)"));
                    }
                }
            m_ShowCapture = GlassInspectorGUI.Foldout(m_ShowCapture, "Background", "Automatic · Quality & refresh");
            if (m_ShowCapture)
                using (new GlassInspectorGUI.Card())
                {
                    GUILayout.Label(selectedGlass.HasBackgroundImage
                        ? "The game background is available."
                        : "Waiting for the game background. A translucent tint is shown until it is available.", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.Space(6f);
                    GlassInspectorGUI.Quality(Property("QualityMode"));
                    EditorGUILayout.PropertyField(Property("SpriteFieldResolution"), new GUIContent("Shape detail", "Resolution of retained sprite silhouettes. Higher values preserve smaller details."));
                    EditorGUILayout.Space(8f);
                    GlassInspectorGUI.Popup(Property("UpdateMode"), "Refresh", new[] { "Real-time", "Camera changes", "Every few frames", "Manual" });
                    var mode = (TranslucentUpdateMode)Property("UpdateMode").intValue;
                    if (mode == TranslucentUpdateMode.Interval && !Property("UpdateMode").hasMultipleDifferentValues)
                        GlassInspectorGUI.Slider(Property("UpdateInterval"), "Frame interval", 1, 120);
                    GUILayout.Label(mode == TranslucentUpdateMode.Always
                        ? "Refreshes every frame for moving backgrounds. Shared across glass surfaces."
                        : "Use Real-time for moving backgrounds. Other modes need camera changes or an explicit refresh.", EditorStyles.wordWrappedMiniLabel);
                    EditorGUILayout.Space(8f);
                    m_ShowBackgroundOverride = EditorGUILayout.Foldout(m_ShowBackgroundOverride, "Advanced background override", true);
                    if (m_ShowBackgroundOverride)
                    {
                        EditorGUILayout.PropertyField(Property("CaptureCamera"), new GUIContent("Camera override", "Optional. Leave empty for automatic background selection."));
                        EditorGUILayout.PropertyField(Property("CaptureLayer"), new GUIContent("Background layer", "Matches the layer on the renderer feature. Use separate layers for stacked glass."));
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField("Resolved camera", selectedGlass.ResolvedCaptureCamera, typeof(Camera), true);
                    }
                }
            m_ShowImageSettings = GlassInspectorGUI.Foldout(m_ShowImageSettings, "Image & Interaction", "Input & masking");
            if (m_ShowImageSettings)
                using (new GlassInspectorGUI.Card())
                {
                    EditorGUILayout.PropertyField(Property("m_Color"), new GUIContent("Graphic color"));
                    GlassInspectorGUI.Toggle(Property("m_RaycastTarget"), "Raycast target");
                    GlassInspectorGUI.Toggle(Property("m_Maskable"), "Maskable");
                    if (Property("m_RaycastTarget").boolValue) GlassInspectorGUI.Slider(Property("SilhouetteRaycastPadding"), "Hit padding", -32f, 64f, "Canvas units around the visible silhouette, within the Image hit rectangle.");
                    if (Property("m_Sprite").objectReferenceValue != null)
                    {
                        GlassInspectorGUI.Popup(Property("m_Type"), "Image type", new[] { "Simple", "Sliced", "Tiled", "Filled" });
                        var imageType = (UnityEngine.UI.Image.Type)Property("m_Type").intValue;
                        if (imageType == UnityEngine.UI.Image.Type.Simple || imageType == UnityEngine.UI.Image.Type.Filled)
                            GlassInspectorGUI.Toggle(Property("m_PreserveAspect"), "Preserve aspect");
                        if (imageType == UnityEngine.UI.Image.Type.Filled)
                        {
                            EditorGUILayout.PropertyField(Property("m_FillMethod"), new GUIContent("Fill method"));
                            GlassInspectorGUI.Slider(Property("m_FillAmount"), "Fill amount", 0f, 1f);
                            int method = Property("m_FillMethod").intValue;
                            string[] origins = method == 0 ? new[] { "Left", "Right" } : method == 1 ? new[] { "Bottom", "Top" } : method == 2 ? new[] { "Bottom left", "Top left", "Top right", "Bottom right" } : new[] { "Bottom", "Left", "Top", "Right" };
                            GlassInspectorGUI.Popup(Property("m_FillOrigin"), "Fill origin", origins);
                            GlassInspectorGUI.Toggle(Property("m_FillClockwise"), "Clockwise");
                        }
                        if (imageType == UnityEngine.UI.Image.Type.Sliced || imageType == UnityEngine.UI.Image.Type.Tiled)
                        {
                            GlassInspectorGUI.Toggle(Property("m_FillCenter"), "Fill center");
                            EditorGUILayout.PropertyField(Property("m_PixelsPerUnitMultiplier"), new GUIContent("Pixel density"));
                        }
                    }
                }
            SessionState.SetBool("LiquidGlass.Inspector.Optics", m_ShowAdvanced);
            SessionState.SetBool("LiquidGlass.Inspector.Capture", m_ShowCapture);
            SessionState.SetBool("LiquidGlass.Inspector.Lighting", m_ShowLighting);
            SessionState.SetBool("LiquidGlass.Inspector.Interaction", m_ShowImageSettings);
            // Apply once after every card. Calling ImageEditor.OnInspectorGUI here would
            // update this SerializedObject again and discard pending slider edits.
            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (Object item in targets) ((TranslucentImageFX)item).SetAllDirty();
                TranslucentRendererFeature.RequestUpdate();
            }
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GlassInspectorGUI.Action("Reset appearance")) ResetAppearance();
                if (GlassInspectorGUI.Action("Setup & help")) TranslucentSetupWindow.ShowWindow();
            }
            EditorGUILayout.LabelField("Reset keeps your shapes, fusion, and capture settings.", EditorStyles.centeredGreyMiniLabel);
        }

        private void DrawFusionActions()
        {
            EditorGUILayout.Space(4f);
            using (new GlassInspectorGUI.Card())
            {
                GlassInspectorGUI.Title("Fusion", "Join nearby shapes into one continuous glass surface.");
                TranslucentImageFX[] selected = System.Array.ConvertAll(targets, item => (TranslucentImageFX)item);
                TranslucentImageFX[] candidates = selected.Length > 1 ? selected : TranslucentFusionAuthoring.FindOverlappingStandaloneGlass(selected[0]);
                if (candidates.Length == 0) candidates = selected;
                string label = candidates.Length > 1 ? $"Fuse {candidates.Length} shapes" : "Create fusion group";
                bool canFuse = TranslucentFusionAuthoring.CanFuse(candidates, out string error);
                using (new EditorGUI.DisabledScope(!canFuse))
                    if (GlassInspectorGUI.Action(label, true))
                    {
                        serializedObject.ApplyModifiedProperties();
                        if (TranslucentFusionAuthoring.FuseWithFeedback(candidates)) GUIUtility.ExitGUI();
                    }
                GUILayout.Label(canFuse ? "Move members together to merge. Move them apart to separate." : error, EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void DrawGroup(TranslucentGlassGroup group)
        {
            using (new GlassInspectorGUI.Card())
            {
                int additions = 0, cutouts = 0;
                foreach (var member in group.Members)
                    if (member != null && member.isActiveAndEnabled && member.Include)
                    { if (member.Operation == GlassOperation.Cutout) cutouts++; else additions++; }
                GlassInspectorGUI.Title("Fusion", $"{additions} additions · {cutouts} cutouts · One surface");
                if (targets.Length == 1) m_CompositionMap.Draw(group, Repaint);
                EditorGUILayout.Space(7f);
                GlassInspectorGUI.FeaturedSlider(Property("m_FusionSoftness"), "Blend distance", 0f, 96f, "Exact joins", "Soft bridges");
                GlassInspectorGUI.Slider(Property("m_SurfacePadding"), "Surface expansion", -32f, 64f);
                EditorGUILayout.Space(6f);
                int number = 0;
                foreach (TranslucentGlassMember member in group.Members)
                {
                    if (member == null) continue;
                    number++;
                    GlassInspectorGUI.MemberRow(member, number);
                }
                if (group.ActiveMemberCount == 0) EditorGUILayout.HelpBox("Add a shape to start the surface.", MessageType.Info);
                using (new EditorGUI.DisabledScope(targets.Length > 1))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GlassInspectorGUI.Action("+ Add shape", true)) AddMember(group, false);
                        if (GlassInspectorGUI.Action("− Add cutout")) AddMember(group, true);
                    }
                GUILayout.Label("Additions join first. Cutouts subtract from the whole surface. Members inherit appearance unless customized.", EditorStyles.wordWrappedMiniLabel);
            }
        }

        private void AddMember(TranslucentGlassGroup group, bool cutout)
        {
            serializedObject.ApplyModifiedProperties();
            TranslucentGlassMember member = TranslucentFusionAuthoring.AddMember(group);
            if (member != null && cutout)
            {
                Undo.RecordObject(member, "Make glass cutout");
                Undo.RecordObject(member.gameObject, "Name glass cutout");
                member.Operation = GlassOperation.Cutout;
                member.Shape = EdgeShape.Circle;
                member.name = "Glass Cutout";
                EditorUtility.SetDirty(member);
            }
            GUIUtility.ExitGUI();
        }

        private void ResetAppearance()
        {
            Undo.RecordObjects(targets, "Reset Glass Appearance");
            foreach (Object item in targets)
            {
                ((TranslucentImageFX)item).ApplyLiquidGlass();
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                EditorUtility.SetDirty(item);
            }
            serializedObject.Update();
        }
    }
}
