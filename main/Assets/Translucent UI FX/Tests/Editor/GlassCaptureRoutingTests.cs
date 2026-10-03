using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace TranslucentUIFX.Tests.Editor
{
    public sealed class GlassCaptureRoutingTests
    {
        [UnityTest]
        public IEnumerator TwoCamerasKeepSeparateCapturesAndMipChains()
        {
            var current = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.NotNull(current);
            var settings = new SerializedObject(current);
            int index = settings.FindProperty("m_DefaultRendererIndex").intValue;
            var template = settings.FindProperty("m_RendererDataList").GetArrayElementAtIndex(index).objectReferenceValue as ScriptableRendererData;
            var data = Object.Instantiate(template);
            data.hideFlags = HideFlags.HideAndDontSave; data.rendererFeatures.Clear();
            var feature = ScriptableObject.CreateInstance<TranslucentRendererFeature>();
            feature.settings.injectionPoint = TranslucentInjectionPoint.BeforeRenderingTransparents;
            feature.settings.backdropMipLevels = 3;
            var later = ScriptableObject.CreateInstance<TranslucentRendererFeature>();
            later.settings.captureLayer = 1; later.settings.injectionPoint = TranslucentInjectionPoint.AfterRenderingTransparents;
            data.rendererFeatures.Add(feature); data.rendererFeatures.Add(later); data.SetDirty();
            var pipeline = UniversalRenderPipelineAsset.Create(data);
            pipeline.hideFlags = HideFlags.HideAndDontSave;
            RenderPipelineAsset previous = QualitySettings.renderPipeline;
            GameObject redObject = null, blueObject = null, redGlassObject = null, blueGlassObject = null, backCanvas = null, frontGlassObject = null;
            RenderTexture redTarget = null, blueTarget = null;
            try
            {
                QualitySettings.renderPipeline = pipeline;
                redObject = new GameObject("Red capture test", typeof(Camera));
                blueObject = new GameObject("Blue capture test", typeof(Camera));
                Camera red = redObject.GetComponent<Camera>(), blue = blueObject.GetComponent<Camera>();
                red.enabled = blue.enabled = false; red.cullingMask = blue.cullingMask = 0;
                red.clearFlags = blue.clearFlags = CameraClearFlags.SolidColor;
                red.backgroundColor = Color.red; blue.backgroundColor = Color.blue;
                redTarget = new RenderTexture(128, 128, 24); blueTarget = new RenderTexture(128, 128, 24);
                redTarget.Create(); blueTarget.Create(); red.targetTexture = redTarget; blue.targetTexture = blueTarget;
                redGlassObject = new GameObject("Red consumer", typeof(RectTransform), typeof(CanvasRenderer));
                blueGlassObject = new GameObject("Blue consumer", typeof(RectTransform), typeof(CanvasRenderer));
                var redGlass = redGlassObject.AddComponent<TranslucentImageFX>();
                var blueGlass = blueGlassObject.AddComponent<TranslucentImageFX>();
                redGlass.CaptureCamera = red; blueGlass.CaptureCamera = blue;
                redGlass.BlurStrength = blueGlass.BlurStrength = 0;
                yield return null;
                redGlass.GlassIntensity = 0;
                red.Render();
                Assert.AreEqual(0f, redGlass.materialForRendering.GetFloat("_CaptureBound"), "Fully faded consumers must not allocate a background pass.");
                redGlass.GlassIntensity = 1;
                red.Render(); blue.Render();
                var redMaterial = redGlass.materialForRendering;
                var blueMaterial = blueGlass.materialForRendering;
                var redCapture = redMaterial.GetTexture("_GlassSourceTex") as RenderTexture;
                var blueCapture = blueMaterial.GetTexture("_GlassSourceTex") as RenderTexture;
                Assert.IsNotNull(redCapture); Assert.IsNotNull(blueCapture);
                Assert.AreNotSame(redCapture, blueCapture);
                Assert.Greater(redCapture.mipmapCount, 1);
                Color redPixel = ReadCenter(redCapture), bluePixel = ReadCenter(blueCapture);
                Assert.Greater(redPixel.r, redPixel.b + 0.5f);
                Assert.Greater(bluePixel.b, bluePixel.r + 0.5f);
                red.Render();
                Assert.AreSame(blueCapture, blueGlass.materialForRendering.GetTexture("_GlassSourceTex"), "Rendering another camera must not replace a material's routed source.");
                blueGlass.BlurStrength = 0.6f;
                blueGlass.UpdateMode = TranslucentUpdateMode.Manual;
                TranslucentRendererFeature.RequestUpdate(); blue.Render();
                float retainedBlur = blueGlass.materialForRendering.GetVector("_CaptureOptions").x;
                Assert.That(retainedBlur, Is.EqualTo(0.6f).Within(0.001f));
                blueGlass.BlurStrength = 0.1f; blue.Render();
                Assert.That(blueGlass.materialForRendering.GetVector("_CaptureOptions").x, Is.EqualTo(retainedBlur).Within(0.001f), "Frozen captures must retain the blur strength they were generated with.");
                // A later capture on the SAME camera must include camera-rendered UI.
                red.cullingMask = 1 << 31;
                backCanvas = new GameObject("Back UI capture fixture", typeof(RectTransform), typeof(Canvas)); backCanvas.layer = 31;
                Canvas canvas = backCanvas.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = red; canvas.planeDistance = 2f;
                var imageObject = new GameObject("Green UI", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)); imageObject.layer = 31;
                imageObject.transform.SetParent(backCanvas.transform, false);
                var image = imageObject.GetComponent<Image>(); image.color = Color.green;
                image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one; image.rectTransform.sizeDelta = Vector2.zero;
                frontGlassObject = new GameObject("Front consumer", typeof(RectTransform), typeof(CanvasRenderer));
                var front = frontGlassObject.AddComponent<TranslucentImageFX>(); front.CaptureCamera = red; front.CaptureLayer = 1; front.BlurStrength = 0f;
                yield return null; Canvas.ForceUpdateCanvases(); red.Render();
                var earlyCapture = redGlass.materialForRendering.GetTexture("_GlassSourceTex") as RenderTexture;
                var laterCapture = front.materialForRendering.GetTexture("_GlassSourceTex") as RenderTexture;
                Assert.AreNotSame(earlyCapture, laterCapture);
                Color earlyPixel = ReadCenter(earlyCapture), laterPixel = ReadCenter(laterCapture);
                Assert.Greater(earlyPixel.r, earlyPixel.g + 0.5f);
                Assert.Greater(laterPixel.g, laterPixel.r + 0.5f, "The later layer must capture the back Canvas.");
            }
            finally
            {
                QualitySettings.renderPipeline = previous;
                if (frontGlassObject != null) Object.DestroyImmediate(frontGlassObject);
                if (backCanvas != null) Object.DestroyImmediate(backCanvas);
                if (redGlassObject != null) Object.DestroyImmediate(redGlassObject);
                if (blueGlassObject != null) Object.DestroyImmediate(blueGlassObject);
                if (redObject != null) Object.DestroyImmediate(redObject);
                if (blueObject != null) Object.DestroyImmediate(blueObject);
                if (redTarget != null) { redTarget.Release(); Object.DestroyImmediate(redTarget); }
                if (blueTarget != null) { blueTarget.Release(); Object.DestroyImmediate(blueTarget); }
                Object.DestroyImmediate(pipeline); Object.DestroyImmediate(feature); Object.DestroyImmediate(later); Object.DestroyImmediate(data);
            }
            yield return null;
        }
        private static Color ReadCenter(RenderTexture texture)
        {
            var previous = RenderTexture.active;
            var pixels = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            try
            {
                RenderTexture.active = texture;
                pixels.ReadPixels(new Rect(texture.width / 2, texture.height / 2, 1, 1), 0, 0); pixels.Apply();
                return pixels.GetPixel(0, 0);
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
        }
    }
}
