using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TranslucentUIFX.Tests.Editor
{
    public sealed class GlassAutomaticBackgroundTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private Camera[] previous;
        private Canvas canvas;
        private TranslucentImageFX glass;
        [SetUp] public void Setup()
        {
            previous = Camera.allCameras;
            foreach (var camera in previous) camera.enabled = false;
            var root = Make("Automatic background", typeof(RectTransform), typeof(Canvas));
            canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var pane = Make("Pane", typeof(RectTransform)); pane.transform.SetParent(root.transform, false);
            glass = pane.AddComponent<TranslucentImageFX>();
        }
        [TearDown] public void Cleanup()
        {
            for (int i = objects.Count - 1; i >= 0; i--) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            foreach (var camera in previous) if (camera != null) camera.enabled = true;
        }
        private GameObject Make(string name, params System.Type[] types)
        { var go = new GameObject(name, types); objects.Add(go); return go; }
        private Camera CreateCamera(string name) => Make(name, typeof(Camera)).GetComponent<Camera>();

        [Test] public void FindsUntaggedWorldViewAndSkipsMinimapOverlayAndOtherDisplays()
        {
            var world = CreateCamera("Untagged world");
            var minimap = CreateCamera("Minimap"); minimap.rect = new Rect(0, 0, 0.2f, 0.2f); minimap.depth = 100;
            var overlay = CreateCamera("Overlay"); overlay.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay; overlay.depth = 200;
            var other = CreateCamera("Other display"); other.targetDisplay = 1; other.depth = 300;
            Assert.AreSame(world, glass.ResolvedCaptureCamera);
            world.enabled = false;
            Assert.AreSame(minimap, glass.ResolvedCaptureCamera);
            minimap.enabled = false;
            Assert.IsNull(glass.ResolvedCaptureCamera);
        }
        [Test] public void RenderTextureCameraDoesNotBecomeOverlayBackground()
        {
            var world = CreateCamera("World"); var offscreen = CreateCamera("Offscreen");
            var target = new RenderTexture(16, 16, 0);
            try { offscreen.targetTexture = target; offscreen.tag = "MainCamera"; Assert.AreSame(world, glass.ResolvedCaptureCamera); }
            finally { offscreen.targetTexture = null; Object.DestroyImmediate(target); }
        }
        [Test] public void CanvasAndExplicitOverridesRetainTheirOwnership()
        {
            var world = CreateCamera("World"); var owner = CreateCamera("Canvas owner"); var manual = CreateCamera("Override");
            world.tag = "MainCamera";
            Assert.AreSame(world, glass.ResolvedCaptureCamera);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = owner;
            Assert.AreSame(owner, glass.ResolvedCaptureCamera);
            glass.CaptureCamera = manual; Assert.AreSame(manual, glass.ResolvedCaptureCamera);
        }
        [Test] public void AddingDisablingAndReparentingSharedSourceUpdatesExistingPanels()
        {
            var world = CreateCamera("World"); world.tag = "MainCamera"; var alternate = CreateCamera("Alternate");
            Assert.AreSame(world, glass.ResolvedCaptureCamera);
            var source = canvas.gameObject.AddComponent<GlassCaptureSource>(); source.Camera = alternate; source.Layer = 3;
            Assert.AreSame(alternate, glass.ResolvedCaptureCamera); Assert.AreEqual(3, glass.ResolvedCaptureLayer);
            source.enabled = false; Assert.AreSame(world, glass.ResolvedCaptureCamera);
            source.enabled = true; Assert.AreSame(alternate, glass.ResolvedCaptureCamera);
            var otherCanvas = Make("Other Canvas", typeof(RectTransform), typeof(Canvas));
            glass.transform.SetParent(otherCanvas.transform, false);
            Assert.AreSame(world, glass.ResolvedCaptureCamera); Assert.AreEqual(0, glass.ResolvedCaptureLayer);
        }
    }
}
