using NUnit.Framework;
using TranslucentUIFX.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TranslucentUIFX.Tests.Editor
{
    public sealed class TranslucentFusionAuthoringTests
    {
        private GameObject m_CanvasObject;
        private Object[] m_PreviousSelection;
        private int m_UndoBoundary;
        private Scene m_PreviousScene;
        private Scene m_TestScene;
        private bool m_OwnsTestScene;

        [SetUp]
        public void SetUp()
        {
            m_PreviousSelection = Selection.objects;
            m_PreviousScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(m_PreviousScene.path))
            {
                // Batch mode starts with an untitled scene and Unity does not allow another
                // scene to be opened additively beside it. Reuse that scene; every object in
                // this fixture is DontSave and is destroyed during teardown.
                m_TestScene = m_PreviousScene;
                m_OwnsTestScene = false;
            }
            else
            {
                m_TestScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(m_TestScene);
                m_OwnsTestScene = true;
            }
            Undo.IncrementCurrentGroup();
            m_UndoBoundary = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Fusion Authoring Test Boundary");

            m_CanvasObject = new GameObject(
                "Fusion Authoring Test Canvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(GraphicRaycaster));
            m_CanvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        }

        [TearDown]
        public void TearDown()
        {
            Undo.RevertAllDownToGroup(m_UndoBoundary);
            Selection.objects = m_PreviousSelection;
            if (m_CanvasObject != null)
                Object.DestroyImmediate(m_CanvasObject);
            if (m_OwnsTestScene && m_TestScene.IsValid())
                EditorSceneManager.CloseScene(m_TestScene, true);
            if (m_PreviousScene.IsValid() && m_PreviousScene.isLoaded)
                SceneManager.SetActiveScene(m_PreviousScene);
        }

        [Test]
        public void FuseCreatesOneGroupAndPreservesInteraction()
        {
            TranslucentImageFX first = CreateGlass("First", new Vector2(-70f, 15f));
            TranslucentImageFX second = CreateGlass("Second", new Vector2(80f, -10f));
            first.ApplyPreset(GlassPreset.LiquidGlass);
            first.BlurStrength = 0.17f;
            first.TintColor = new Color(0.2f, 0.6f, 1f, 0.22f);
            first.EnableSpecularGlare = false;
            Button button = first.gameObject.AddComponent<Button>();
            button.targetGraphic = first;
            GameObject firstObject = first.gameObject;
            GameObject secondObject = second.gameObject;

            bool success = TranslucentFusionAuthoring.TryFuse(
                new[] { first, second },
                out TranslucentGlassGroup group,
                out string error);

            Assert.IsTrue(success, error);
            Assert.NotNull(group);
            Assert.AreEqual(2, group.ActiveMemberCount);
            Assert.AreEqual(m_CanvasObject.transform, group.transform.parent);
            Assert.AreEqual(group.transform, firstObject.transform.parent);
            Assert.AreEqual(group.transform, secondObject.transform.parent);
            Assert.IsNull(firstObject.GetComponent<TranslucentImageFX>());
            Assert.AreEqual(typeof(Image), firstObject.GetComponent<Image>().GetType());
            Assert.NotNull(firstObject.GetComponent<TranslucentGlassMember>());
            Assert.AreSame(firstObject.GetComponent<Image>(), button.targetGraphic);
            Assert.AreEqual(0.17f, group.BlurStrength, 0.0001f);
            Assert.AreEqual(new Color(0.2f, 0.6f, 1f, 0.22f), group.TintColor);
            Assert.IsFalse(group.EnableSpecularGlare);
            Assert.IsFalse(group.raycastTarget);
            Assert.IsNotEmpty(firstObject.GetComponent<TranslucentGlassMember>().EditorStandaloneJson);
        }

        [Test]
        public void DetachRestoresTheOriginalStandaloneGlassState()
        {
            TranslucentImageFX first = CreateGlass("First", new Vector2(-40f, 0f));
            TranslucentImageFX second = CreateGlass("Second", new Vector2(60f, 0f));
            first.ApplyLiquidGlass();
            first.GlassIntensity = 0.63f;
            first.EnableSpecularGlare = true;
            Button button = first.gameObject.AddComponent<Button>();
            button.targetGraphic = first;
            GameObject firstObject = first.gameObject;

            Assert.IsTrue(
                TranslucentFusionAuthoring.TryFuse(new[] { first, second }, out TranslucentGlassGroup group, out string fuseError),
                fuseError);

            TranslucentGlassMember member = firstObject.GetComponent<TranslucentGlassMember>();
            Assert.IsTrue(
                TranslucentFusionAuthoring.TryDetach(new[] { member }, out TranslucentImageFX[] detached, out string detachError),
                detachError);

            Assert.AreEqual(1, detached.Length);
            Assert.AreSame(firstObject, detached[0].gameObject);
            Assert.AreEqual(GlassPreset.LiquidGlass, detached[0].CurrentPreset);
            Assert.AreEqual(0.63f, detached[0].GlassIntensity, 0.0001f);
            Assert.IsTrue(detached[0].EnableSpecularGlare);
            Assert.AreEqual(m_CanvasObject.transform, firstObject.transform.parent);
            Assert.AreSame(detached[0], button.targetGraphic);
            Assert.AreEqual(1, group.ActiveMemberCount);
        }

        [Test]
        public void FusionKeepsIndependentOpacityAndGraphicColorEvenWhenFirstSourceIsHidden()
        {
            var first = CreateGlass("Hidden", new Vector2(-60f, 0f));
            var second = CreateGlass("Visible", new Vector2(60f, 0f));
            first.GlassIntensity = 0f; first.color = Color.red;
            second.GlassIntensity = 0.8f; second.color = new Color(0.3f, 0.7f, 0.9f, 0.6f);
            var firstObject = first.gameObject; var secondObject = second.gameObject;
            Assert.IsTrue(TranslucentFusionAuthoring.TryFuse(new[] { first, second }, out var group, out var error), error);
            Assert.AreEqual(1f, group.GlassIntensity);
            Assert.AreEqual(Color.white, group.color);
            var hidden = firstObject.GetComponent<TranslucentGlassMember>();
            var visible = secondObject.GetComponent<TranslucentGlassMember>();
            Assert.IsTrue(hidden.Appearance.Override);
            Assert.AreEqual(0f, hidden.Appearance.Opacity);
            Assert.AreEqual(0.8f, visible.Appearance.Opacity);
            Assert.IsTrue(TranslucentFusionAuthoring.TryDetach(new[] { visible }, out var detached, out error), error);
            Assert.AreEqual(new Color(0.3f, 0.7f, 0.9f, 0.6f), detached[0].color);
            Assert.AreEqual(0.8f, detached[0].GlassIntensity);
        }

        [Test]
        public void OverlapSuggestionFindsOnlyEligibleSiblings()
        {
            TranslucentImageFX first = CreateGlass("First", Vector2.zero);
            TranslucentImageFX overlapping = CreateGlass("Overlapping", new Vector2(80f, 0f));
            CreateGlass("Far Away", new Vector2(700f, 0f));

            TranslucentImageFX[] matches = TranslucentFusionAuthoring.FindOverlappingStandaloneGlass(first);

            CollectionAssert.AreEquivalent(new[] { first, overlapping }, matches);
        }

        [Test]
        public void CanFuseRejectsDifferentParentsWithoutMutation()
        {
            TranslucentImageFX first = CreateGlass("First", Vector2.zero);
            TranslucentImageFX second = CreateGlass("Second", Vector2.zero);
            GameObject otherParent = new GameObject("Other Parent", typeof(RectTransform));
            otherParent.transform.SetParent(m_CanvasObject.transform, false);
            second.transform.SetParent(otherParent.transform, false);

            bool canFuse = TranslucentFusionAuthoring.CanFuse(new[] { first, second }, out string error);

            Assert.IsFalse(canFuse);
            StringAssert.Contains("siblings", error);
            Object.DestroyImmediate(otherParent);
        }

        [Test]
        public void NewGlassHasTheSameOpticsAsResetGlass()
        {
            TranslucentImageFX glass = CreateGlass("New Glass", Vector2.zero);
            float blur = glass.BlurStrength;
            float refraction = glass.RefractionAmount;
            Color tint = glass.TintColor;
            glass.ApplyLiquidGlass();
            Assert.AreEqual(GlassPreset.LiquidGlass, glass.CurrentPreset);
            Assert.AreEqual(blur, glass.BlurStrength);
            Assert.AreEqual(refraction, glass.RefractionAmount);
            Assert.AreEqual(tint, glass.TintColor);
            Assert.IsTrue(glass.ProceduralShape);
            Assert.AreEqual(EdgeShape.ContinuousRoundedRect, glass.EdgeShape);
        }

        [Test]
        public void ResetAppearancePreservesGeometryAndCaptureSettings()
        {
            TranslucentImageFX glass = CreateGlass("Customized Glass", Vector2.zero);
            glass.EdgeShape = EdgeShape.Capsule;
            glass.EdgeRounding = 0.37f;
            glass.ProceduralShape = false;
            glass.UpdateMode = TranslucentUpdateMode.Manual;
            glass.QualityMode = PerformanceMode.Low;
            glass.EnableSpecularGlare = false;
            glass.FrostAmount = 0.9f;
            glass.ApplyLiquidGlass();
            Assert.AreEqual(EdgeShape.Capsule, glass.EdgeShape);
            Assert.AreEqual(0.37f, glass.EdgeRounding);
            Assert.IsFalse(glass.ProceduralShape);
            Assert.AreEqual(TranslucentUpdateMode.Manual, glass.UpdateMode);
            Assert.AreEqual(PerformanceMode.Low, glass.QualityMode);
            Assert.IsTrue(glass.EnableSpecularGlare);
            Assert.AreEqual(0f, glass.FrostAmount);
        }

        [TestCase(GlassPreset.DefaultGlass)]
        [TestCase(GlassPreset.SoftFrost)]
        [TestCase(GlassPreset.DarkGlass)]
        [TestCase(GlassPreset.WhiteGlass)]
        [TestCase(GlassPreset.StrongBlur)]
        public void RetiredPresetCallsResolveToLiquidGlass(GlassPreset retired)
        {
            TranslucentImageFX glass = CreateGlass("Legacy Caller", Vector2.zero);
            glass.ApplyPreset(retired);
            Assert.AreEqual(GlassPreset.LiquidGlass, glass.CurrentPreset);
            Assert.AreEqual(0f, glass.FrostAmount);
            Assert.IsTrue(glass.EnableSpecularGlare);
        }

        [Test]
        public void DemoControlsChangeOpticsResetAndCycleAllShapes()
        {
            TranslucentImageFX glass = CreateGlass("Demo Glass", Vector2.zero);
            glass.EdgeShape = EdgeShape.Capsule;
            TranslucentImageFX preview = CreateGlass("Shape Preview", Vector2.zero);
            GameObject owner = new GameObject("Demo Controls");
            owner.transform.SetParent(m_CanvasObject.transform, false);
            owner.SetActive(false);
            var controller = owner.AddComponent<TranslucentUIFX.Demo.LiquidGlassDemoController>();
            Slider blur = new GameObject("Blur", typeof(RectTransform), typeof(Slider)).GetComponent<Slider>();
            Slider bend = new GameObject("Bend", typeof(RectTransform), typeof(Slider)).GetComponent<Slider>();
            Toggle pointer = new GameObject("Pointer", typeof(RectTransform), typeof(Toggle)).GetComponent<Toggle>();
            Button reset = new GameObject("Reset", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            Button shape = new GameObject("Shape", typeof(RectTransform), typeof(Button)).GetComponent<Button>();
            foreach (var control in new Component[] { blur, bend, pointer, reset, shape })
                control.transform.SetParent(owner.transform, false);
            var data = new SerializedObject(controller);
            data.FindProperty("target").objectReferenceValue = glass;
            data.FindProperty("shapePreview").objectReferenceValue = preview;
            data.FindProperty("blurSlider").objectReferenceValue = blur;
            data.FindProperty("refractionSlider").objectReferenceValue = bend;
            data.FindProperty("pointerToggle").objectReferenceValue = pointer;
            data.FindProperty("resetButton").objectReferenceValue = reset;
            data.FindProperty("shapeButton").objectReferenceValue = shape;
            data.ApplyModifiedPropertiesWithoutUndo();
            owner.SetActive(true);
            blur.value = 0.4f;
            bend.value = 0.065f;
            pointer.isOn = true;
            Assert.AreEqual(0.4f, glass.BlurStrength);
            Assert.AreEqual(0.065f, glass.RefractionAmount);
            Assert.IsTrue(glass.InteractiveGlare);
            reset.onClick.Invoke();
            Assert.AreEqual(0.08f, glass.BlurStrength);
            Assert.AreEqual(0.035f, glass.RefractionAmount);
            Assert.IsFalse(glass.InteractiveGlare);
            Assert.AreEqual(EdgeShape.Capsule, glass.EdgeShape);
            foreach (EdgeShape expected in new[] { EdgeShape.RoundedRect, EdgeShape.Capsule, EdgeShape.Circle, EdgeShape.Rectangle, EdgeShape.Diamond, EdgeShape.Triangle, EdgeShape.Hexagon, EdgeShape.ContinuousRoundedRect })
            {
                shape.onClick.Invoke();
                Assert.AreEqual(expected, preview.EdgeShape);
                Assert.IsTrue(preview.ProceduralShape);
            }
            owner.SetActive(false);
            blur.value = 0.9f;
            Assert.AreEqual(0.08f, glass.BlurStrength, "Disabled controllers must detach their listeners.");
        }

        [Test]
        public void AlphaGradientStabilityIsEnabledAndSentToTheShader()
        {
            TranslucentImageFX glass = CreateGlass("Soft Alpha Glass", Vector2.zero);

            Assert.IsTrue(glass.StabilizeAlphaGradients);
            Material stableMaterial = glass.materialForRendering;
            Assert.IsTrue(stableMaterial.HasProperty("_AlphaGradientStability"));
            Assert.AreEqual(1f, stableMaterial.GetFloat("_AlphaGradientStability"), 0.0001f);

            glass.StabilizeAlphaGradients = false;
            glass.SetMaterialDirty();
            Material legacyMaterial = glass.materialForRendering;
            Assert.AreEqual(0f, legacyMaterial.GetFloat("_AlphaGradientStability"), 0.0001f);
        }

        [Test]
        public void FusionPreservesAlphaGradientStability()
        {
            TranslucentImageFX first = CreateGlass("Stable Alpha Source", new Vector2(-50f, 0f));
            TranslucentImageFX second = CreateGlass("Stable Alpha Partner", new Vector2(50f, 0f));
            first.StabilizeAlphaGradients = false;

            Assert.IsTrue(
                TranslucentFusionAuthoring.TryFuse(new[] { first, second }, out TranslucentGlassGroup group, out string error),
                error);

            Assert.IsFalse(group.StabilizeAlphaGradients);
        }

        [TestCase(EdgeShape.Rectangle)]
        [TestCase(EdgeShape.Circle)]
        [TestCase(EdgeShape.RoundedRect)]
        [TestCase(EdgeShape.Diamond)]
        [TestCase(EdgeShape.Capsule)]
        [TestCase(EdgeShape.ContinuousRoundedRect)]
        [TestCase(EdgeShape.Triangle)]
        [TestCase(EdgeShape.Hexagon)]
        public void ProceduralSilhouettesContainTheirCenterAndRejectOutsidePoints(EdgeShape shape)
        {
            Assert.Less(GlassShapeUtility.Distance(Vector2.zero, new Vector2(200f, 100f), shape, 0.2f, 0.82f), 0f);
            Assert.Greater(GlassShapeUtility.Distance(new Vector2(110f, 0f), new Vector2(200f, 100f), shape, 0.2f, 0.82f), 0f);
        }

        [Test]
        public void IndividualCornersHaveIndependentHitAreasAndSurviveFusion()
        {
            TranslucentImageFX glass = CreateGlass("Asymmetric", Vector2.zero);
            glass.rectTransform.sizeDelta = new Vector2(100f, 100f);
            glass.EdgeShape = EdgeShape.RoundedRect;
            glass.IndividualCorners = true;
            glass.CornerRadii = new Vector4(0.5f, 0f, 0f, 0f);
            Assert.Greater(GlassShapeUtility.Distance(new Vector2(-49f, 49f), Vector2.one * 100f, glass.EdgeShape, 0.2f, 0f, true, glass.CornerRadii), 0f);
            Assert.Less(GlassShapeUtility.Distance(new Vector2(49f, 49f), Vector2.one * 100f, glass.EdgeShape, 0.2f, 0f, true, glass.CornerRadii), 0f);
            Assert.IsTrue(TranslucentFusionAuthoring.TryFuse(new[] { glass }, out var group, out string error), error);
            var member = group.Members[0];
            Assert.IsTrue(member.IndividualCorners);
            member.Shape = EdgeShape.Hexagon;
            Assert.IsTrue(TranslucentFusionAuthoring.TryDetach(new[] { member }, out var detached, out error), error);
            Assert.AreEqual(EdgeShape.Hexagon, detached[0].EdgeShape, "Detach preserves the geometry edited while fused.");
            Assert.AreEqual(new Vector4(0.5f, 0f, 0f, 0f), detached[0].CornerRadii);
        }

        [Test]
        public void FusionGrowsBeyondEightMembersWithoutDroppingGeometry()
        {
            var first = CreateGlass("Growing group", Vector2.zero);
            Assert.IsTrue(TranslucentFusionAuthoring.TryFuse(new[] { first }, out var group, out string error), error);
            group.FusionSoftness = 0; group.SurfacePadding = 0;
            TranslucentGlassMember last = null;
            for (int i = 1; i < 17; i++)
            {
                last = TranslucentFusionAuthoring.AddMember(group);
                Assert.IsNotNull(last);
                last.RectTransform.anchoredPosition = new Vector2(i * 400f, 0f);
            }
            group.RefreshMembers();
            Assert.AreEqual(17, group.ActiveMemberCount);
            Assert.AreEqual(17, group.materialForRendering.GetFloat("_FusionCount"));
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, last.RectTransform.TransformPoint(Vector2.zero));
            Assert.IsTrue(group.ContainsScreenPoint(point, null));
        }

        [Test]
        public void SpriteFusionPreservesSilhouetteCutoutsAndDetach()
        {
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color32[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                pixels[y * 32 + x] = new Color32(255, 255, 255, (byte)(x < 10 || y < 10 ? 255 : 0));
            texture.SetPixels32(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
            try
            {
                var glass = CreateGlass("Sprite L", Vector2.zero);
                glass.sprite = sprite; glass.preserveAspect = true;
                Assert.IsTrue(TranslucentFusionAuthoring.TryFuse(new[] { glass }, out var group, out string error), error);
                group.FusionSoftness = 0; group.SurfacePadding = 0;
                var member = group.Members[0];
                Assert.AreSame(sprite, member.ShapeSprite);
                Assert.IsTrue(member.PreserveAspect);
                Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, member.transform.TransformPoint(new Vector2(-40, 40)));
                Vector2 outside = RectTransformUtility.WorldToScreenPoint(null, member.transform.TransformPoint(new Vector2(40, 40)));
                Assert.IsTrue(group.ContainsScreenPoint(inside, null));
                Assert.IsFalse(group.ContainsScreenPoint(outside, null));
                Assert.IsNotNull(group.materialForRendering.GetTexture("_FusionSpriteFields"));
                var hole = TranslucentFusionAuthoring.AddMember(group);
                hole.ShapeSprite = sprite; hole.Operation = GlassOperation.Cutout;
                hole.RectTransform.sizeDelta = member.ShapeRect.size;
                hole.RectTransform.localPosition = member.RectTransform.localPosition;
                Assert.IsFalse(group.ContainsScreenPoint(inside, null));
                hole.Include = false;
                Assert.IsTrue(group.ContainsScreenPoint(inside, null));
                Assert.IsTrue(TranslucentFusionAuthoring.TryDetach(new[] { member }, out var restored, out error), error);
                Assert.AreSame(sprite, restored[0].sprite);
                Assert.IsTrue(restored[0].preserveAspect);
                Assert.IsTrue(restored[0].UsesSpriteSilhouette);
            }
            finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
        }

        [Test]
        public void FusionRespectsRotationAndCutouts()
        {
            var glass = CreateGlass("Rotated", Vector2.zero);
            glass.EdgeShape = EdgeShape.Rectangle;
            Assert.IsTrue(TranslucentFusionAuthoring.TryFuse(new[] { glass }, out var group, out string error), error);
            group.FusionSoftness = 0f;
            group.SurfacePadding = 0f;
            var member = group.Members[0];
            member.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Vector2 inside = RectTransformUtility.WorldToScreenPoint(null, member.transform.TransformPoint(new Vector2(100f, 0f)));
            Vector2 outside = RectTransformUtility.WorldToScreenPoint(null, member.transform.TransformPoint(new Vector2(0f, 75f)));
            Assert.IsTrue(group.ContainsScreenPoint(inside, null));
            Assert.IsFalse(group.ContainsScreenPoint(outside, null));
            Material material = group.materialForRendering;
            Assert.AreEqual(1f, material.GetFloat("_FusionCount"));
            var hole = TranslucentFusionAuthoring.AddMember(group);
            hole.RectTransform.localPosition = member.RectTransform.localPosition;
            hole.RectTransform.sizeDelta = Vector2.one * 40f;
            hole.Operation = GlassOperation.Cutout;
            hole.Shape = EdgeShape.Circle;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, hole.transform.position);
            Assert.IsFalse(group.ContainsScreenPoint(center, null));
            Assert.IsTrue(group.ContainsScreenPoint(inside, null));
            Assert.IsFalse(member.IsRaycastLocationValid(center, null), "The cutout must not receive the member's pointer input.");
        }

        [Test]
        public void NestedFusionGroupsOwnTheirMembersAndEmptyGroupsStayEmpty()
        {
            var glass = CreateGlass("Outer", Vector2.zero);
            Assert.IsTrue(TranslucentFusionAuthoring.TryFuse(new[] { glass }, out var outer, out string error), error);
            GameObject nestedObject = new GameObject("Nested", typeof(RectTransform), typeof(CanvasRenderer), typeof(TranslucentGlassGroup));
            Undo.RegisterCreatedObjectUndo(nestedObject, "Create nested group fixture");
            nestedObject.transform.SetParent(outer.transform, false);
            var nested = nestedObject.GetComponent<TranslucentGlassGroup>();
            Assert.IsFalse(nested.ContainsScreenPoint(Vector2.zero, null));
            Assert.AreEqual(1f, nested.materialForRendering.GetFloat("_FusionEnabled"));
            Assert.AreEqual(0f, nested.materialForRendering.GetFloat("_FusionCount"));
            TranslucentFusionAuthoring.AddMember(nested);
            outer.RefreshMembers();
            Assert.AreEqual(1, outer.MemberCount);
            Assert.AreEqual(1, nested.MemberCount);
        }

        [Test]
        public void FusionMeshFollowsAMemberBeyondTheOriginalGroupRect()
        {
            var glass = CreateGlass("Moving", Vector2.zero);
            Assert.IsTrue(TranslucentFusionAuthoring.TryFuse(new[] { glass }, out var group, out string error), error);
            group.Members[0].RectTransform.localPosition = new Vector3(500f, 0f, 0f);
            using (var helper = new VertexHelper())
            {
                typeof(TranslucentGlassGroup).GetMethod("OnPopulateMesh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly).Invoke(group, new object[] { helper });
                UIVertex vertex = default;
                helper.PopulateUIVertex(ref vertex, 2);
                Assert.Greater(vertex.position.x, 600f);
                Assert.Greater(vertex.uv1.x, 1f, "Shape UVs must extend beyond the group's layout rectangle.");
            }
        }

        private TranslucentImageFX CreateGlass(string name, Vector2 position)
        {
            GameObject gameObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TranslucentImageFX));
            RectTransform rect = (RectTransform)gameObject.transform;
            rect.SetParent(m_CanvasObject.transform, false);
            rect.sizeDelta = new Vector2(220f, 120f);
            rect.anchoredPosition = position;
            return gameObject.GetComponent<TranslucentImageFX>();
        }
    }
}
