using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace TranslucentUIFX.Tests.Editor
{
    public sealed class GlassRenderingTests
    {
        private GameObject root;
        private Texture2D backdrop;
        private Sprite shapeSprite;
        [SetUp] public void Setup()
        {
            root = new GameObject("Glass rendering fixture", typeof(RectTransform), typeof(CanvasRenderer));
            backdrop = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
            var pixels = new Color[128 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                pixels[y * 128 + x] = new Color((x / 5) % 2 == 0 ? 0.1f : 0.8f, y / 128f, 0.3f, 1f);
            backdrop.SetPixels(pixels); backdrop.Apply();
        }
        [TearDown] public void Cleanup() { Object.DestroyImmediate(root); if (shapeSprite != null) Object.DestroyImmediate(shapeSprite); Object.DestroyImmediate(backdrop); }

        [Test] public void StandaloneDrawsWithoutAFusionGroupAndFadeReachesZero()
        {
            var glass = root.AddComponent<TranslucentImageFX>();
            glass.rectTransform.sizeDelta = Vector2.one * 128;
            glass.EdgeShape = EdgeShape.Circle; glass.TintColor = Color.red;
            Color[] visible = Render(glass);
            Assert.Greater(visible[64 * 128 + 64].r, 0.7f);
            Assert.Greater(visible[64 * 128 + 64].a, 0.8f);
            Assert.Less(visible[4 * 128 + 4].a, 0.05f);
            glass.GlassIntensity = 0f;
            Color[] hidden = Render(glass);
            Assert.Less(hidden[64 * 128 + 64].a, 0.01f);
        }

        [Test] public void MissingBackgroundUsesTranslucentTintAndRecoversWhenAvailable()
        {
            var glass = root.AddComponent<TranslucentImageFX>();
            glass.rectTransform.sizeDelta = Vector2.one * 128;
            glass.TintColor = new Color(1, 1, 1, 0.1f);
            Color missing = Render(glass, backgroundAvailable: false)[64 * 128 + 64];
            Assert.That(missing.a, Is.GreaterThan(0).And.LessThan(0.15f), "No background must not become an opaque placeholder.");
            Assert.Greater(Render(glass)[64 * 128 + 64].a, 0.9f);
            glass.GlassIntensity = 0;
            Assert.Less(Render(glass, backgroundAvailable: false)[64 * 128 + 64].a, 0.001f);
        }

        [Test] public void RefractionMovesBackdropAtTheEdgeAndLeavesCenterClear()
        {
            var glass = root.AddComponent<TranslucentImageFX>();
            glass.rectTransform.sizeDelta = Vector2.one * 128;
            glass.EdgeShape = EdgeShape.Circle; glass.BlurStrength = 0; glass.RefractionAmount = 0;
            glass.Surface.OpticalLip = 18; glass.Surface.LightColor = Color.clear; glass.Surface.LipShadow = Color.clear;
            glass.EnableSpecularGlare = false; glass.TintColor = Color.clear;
            Color[] flat = Render(glass);
            glass.RefractionAmount = 0.1f;
            Color[] bent = Render(glass);
            float edgeDifference = 0; int count = 0;
            for (int y = 10; y < 118; y++) for (int x = 10; x < 118; x++)
            {
                float radius = new Vector2(x - 63.5f, y - 63.5f).magnitude;
                if (radius < 50 || radius > 61) continue;
                edgeDifference += Mathf.Abs(flat[y * 128 + x].r - bent[y * 128 + x].r); count++;
            }
            Assert.Greater(edgeDifference / count, 0.02f, "Refraction must change captured pixels near the boundary.");
            Assert.That(bent[64 * 128 + 64].r, Is.EqualTo(flat[64 * 128 + 64].r).Within(0.01f));
        }

        [Test] public void SpriteAndProceduralMembersRenderOneSurfaceWithCutouts()
        {
            var group = root.AddComponent<TranslucentGlassGroup>();
            group.rectTransform.sizeDelta = Vector2.one * 128;
            group.SurfacePadding = 0; group.FusionSoftness = 16;
            var left = Member(group, "Left", new Vector2(-28, 0), 70, GlassOperation.Add);
            var right = Member(group, "Right", new Vector2(28, 0), 70, GlassOperation.Add);
            shapeSprite = Sprite.Create(backdrop, new Rect(0, 0, 128, 128), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect);
            left.ShapeSprite = shapeSprite;
            left.Appearance.Override = true; left.Appearance.Tint = Color.red;
            right.Appearance.Override = true; right.Appearance.Tint = Color.blue;
            group.RefreshMembers();
            Color[] joined = Render(group);
            Assert.Greater(joined[64 * 128 + 64].a, 0.8f);
            Assert.Greater(joined[64 * 128 + 32].r, joined[64 * 128 + 32].b);
            Assert.Greater(joined[64 * 128 + 96].b, joined[64 * 128 + 96].r);
            var hole = Member(group, "Hole", Vector2.zero, 18, GlassOperation.Cutout);
            group.RefreshMembers();
            Color[] cut = Render(group);
            Assert.Less(cut[64 * 128 + 64].a, 0.05f);
            Assert.Greater(cut[64 * 128 + 32].a, 0.8f);
        }

        [Test] public void FusionBlendsMemberGraphicColorsAndIndependentFade()
        {
            var group = root.AddComponent<TranslucentGlassGroup>();
            group.rectTransform.sizeDelta = Vector2.one * 128; group.SurfacePadding = 0; group.FusionSoftness = 0;
            var left = Member(group, "Hidden", new Vector2(-32, 0), 48, GlassOperation.Add);
            var right = Member(group, "Visible", new Vector2(32, 0), 48, GlassOperation.Add);
            left.Appearance.Override = right.Appearance.Override = true;
            left.Appearance.Opacity = 0; right.Appearance.Opacity = 1;
            right.Appearance.Tint = Color.white; right.Appearance.GraphicColor = Color.green;
            group.RefreshMembers();
            var pixels = Render(group);
            Assert.Less(pixels[64 * 128 + 32].a, 0.01f);
            Assert.Greater(pixels[64 * 128 + 96].g, 0.9f);
            Assert.Less(pixels[64 * 128 + 96].r, 0.01f);
            Assert.Greater(pixels[64 * 128 + 96].a, 0.9f);
            group.GlassIntensity = 0;
            Assert.Less(Render(group)[64 * 128 + 96].a, 0.01f);
        }

        [Test] public void ConcaveCornerProfilesChangeTheRenderedSilhouette()
        {
            var glass = root.AddComponent<TranslucentImageFX>();
            glass.rectTransform.sizeDelta = Vector2.one * 128;
            glass.EdgeShape = EdgeShape.RoundedRect;
            glass.CornerSettings.Enabled = true; glass.CornerSettings.Continuous = false;
            glass.CornerSettings.Radii = Vector4.one * 50; glass.CornerSettings.Curves = Vector4.zero;
            Color[] round = Render(glass);
            glass.CornerSettings.Curves = Vector4.one * 2;
            Color[] concave = Render(glass);
            Assert.Greater(round[104 * 128 + 104].a, 0.8f);
            Assert.Less(concave[104 * 128 + 104].a, 0.05f);
            Assert.Greater(concave[64 * 128 + 64].a, 0.8f);
        }

        [Test] public void DropShadowExtendsBeyondThePaneAndCanBeDisabled()
        {
            var glass = root.AddComponent<TranslucentImageFX>();
            glass.rectTransform.sizeDelta = Vector2.one * 64;
            glass.EdgeShape = EdgeShape.Rectangle;
            glass.Surface.ShadowColor = new Color(0, 0, 0, 0.8f);
            glass.Surface.ShadowSize = 5; glass.Surface.ShadowOffset = new Vector2(6, 0);
            Color[] shadow = Render(glass);
            Assert.Greater(shadow[64 * 128 + 105].a, 0.02f);
            glass.Surface.ShadowColor = Color.clear;
            Color[] clear = Render(glass);
            Assert.Less(clear[64 * 128 + 105].a, 0.01f);
        }

        [Test] public void FusionLeavesNoGhostPixelsOutsideItsSilhouette()
        {
            var group = root.AddComponent<TranslucentGlassGroup>();
            group.rectTransform.sizeDelta = Vector2.one * 128; group.FusionSoftness = 12; group.SurfacePadding = 0;
            Member(group, "Circle", Vector2.zero, 40, GlassOperation.Add); group.RefreshMembers();
            var pixels = Render(group);
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
                if (new Vector2(x - 63.5f, y - 63.5f).magnitude > 24f)
                    Assert.Less(pixels[y * 128 + x].a, 0.01f, $"Unexpected glass outside the circle at {x}, {y}");
        }

        [Test] public void WarmFusionShaderCostStaysWithinEditorBenchmarkBudget()
        {
            var group = root.AddComponent<TranslucentGlassGroup>();
            group.rectTransform.sizeDelta = Vector2.one * 128;
            group.FusionSoftness = 3; group.SurfacePadding = 0;
            Member(group, "First", new Vector2(-56, -56), 14, GlassOperation.Add);
            group.RefreshMembers();
            double single = Measure(group);
            for (int i = 1; i < 64; i++) Member(group, "Member " + i, new Vector2(-56 + (i % 8) * 16, -56 + (i / 8) * 16), 14, GlassOperation.Add);
            group.RefreshMembers();
            double collection = Measure(group);
            string report = $"Device: {SystemInfo.graphicsDeviceName}\n1024 x 1024 direct glass draw plus one-pixel synchronous readback; median of 5 warm samples.\n1 member: {single:0.000} ms (budget 10 ms)\n64 members: {collection:0.000} ms (budget 30 ms)\nThis is an Editor regression benchmark, not a player frame-rate measurement or competitor comparison.\n";
            System.IO.File.WriteAllText("/tmp/liquid-glass-benchmark.txt", report);
            Assert.Less(single, 10, report);
            Assert.Less(collection, 30, report);
        }
        private double Measure(TranslucentImageFX glass)
        {
            Render(glass, 1024, true); Render(glass, 1024, true);
            var samples = new double[5];
            for (int i = 0; i < samples.Length; i++)
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                Render(glass, 1024, true); clock.Stop(); samples[i] = clock.Elapsed.TotalMilliseconds;
            }
            System.Array.Sort(samples); return samples[samples.Length / 2];
        }

        private static TranslucentGlassMember Member(TranslucentGlassGroup group, string name, Vector2 position, float size, GlassOperation operation)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(group.transform, false);
            var member = go.AddComponent<TranslucentGlassMember>();
            member.RectTransform.sizeDelta = Vector2.one * size;
            member.RectTransform.anchoredPosition = position;
            member.Shape = EdgeShape.Circle; member.Operation = operation;
            return member;
        }

        private Color[] Render(TranslucentImageFX glass, int resolution = 128, bool sampleOnly = false, bool backgroundAvailable = true)
        {
            var mesh = new Mesh();
            var render = RenderTexture.GetTemporary(resolution, resolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            int readSize = sampleOnly ? 1 : resolution;
            var readback = new Texture2D(readSize, readSize, TextureFormat.RGBA32, false, true);
            RenderTexture previous = RenderTexture.active;
            Texture oldSource = Shader.GetGlobalTexture("_TranslucentUI_SourceTex");
            Texture oldBlur = Shader.GetGlobalTexture("_TranslucentUI_BlurredTex");
            float oldPreview = Shader.GetGlobalFloat("_TranslucentUI_SceneViewPreview");
            Vector4 oldScreen = Shader.GetGlobalVector("_ScreenParams");
            try
            {
                using (var helper = new VertexHelper())
                {
                    glass.GetType().GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(VertexHelper) }, null).Invoke(glass, new object[] { helper });
                    helper.FillMesh(mesh);
                }
                var material = glass.materialForRendering;
                material.SetTexture("_MainTex", glass.mainTexture);
                material.SetFloat("_CaptureBound", 0f);
                material.SetFloat("_CaptureUnavailable", backgroundAvailable ? 0f : 1f); // This fixture supplies a captured backdrop explicitly.
                using (var commands = new CommandBuffer())
                {
                    commands.SetRenderTarget(render); commands.ClearRenderTarget(true, true, Color.clear);
                    commands.SetViewProjectionMatrices(Matrix4x4.identity, GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-64, 64, -64, 64, -1, 1), true));
                    commands.SetGlobalTexture("_TranslucentUI_SourceTex", backdrop);
                    commands.SetGlobalTexture("_TranslucentUI_BlurredTex", backdrop);
                    commands.SetGlobalFloat("_TranslucentUI_SceneViewPreview", 0);
                    commands.SetGlobalVector("_ScreenParams", new Vector4(resolution, resolution, 1f + 1f / resolution, 1f + 1f / resolution));
                    commands.DrawMesh(mesh, Matrix4x4.identity, material);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = render;
                readback.ReadPixels(new Rect(sampleOnly ? resolution / 2 : 0, sampleOnly ? resolution / 2 : 0, readSize, readSize), 0, 0); readback.Apply();
                return readback.GetPixels();
            }
            finally
            {
                Shader.SetGlobalTexture("_TranslucentUI_SourceTex", oldSource); Shader.SetGlobalTexture("_TranslucentUI_BlurredTex", oldBlur);
                Shader.SetGlobalFloat("_TranslucentUI_SceneViewPreview", oldPreview); Shader.SetGlobalVector("_ScreenParams", oldScreen);
                RenderTexture.active = previous; RenderTexture.ReleaseTemporary(render);
                Object.DestroyImmediate(mesh); Object.DestroyImmediate(readback);
            }
        }
    }
}
