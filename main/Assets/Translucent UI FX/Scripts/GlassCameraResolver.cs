using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace TranslucentUIFX
{
    /// <summary>Shared automatic routing for ordinary UI. Never creates or changes cameras.</summary>
    public static class GlassCameraResolver
    {
        private static readonly Dictionary<Canvas, Camera> Results = new Dictionary<Canvas, Camera>();
        private static Camera[] cameras = new Camera[8];
        private static int frame = -1, cameraCount;

        public static Camera Resolve(Canvas canvas)
        {
            var root = canvas != null ? canvas.rootCanvas : null;
            // Camera/world-space UI already declares which view owns its coordinates.
            if (root != null && root.renderMode != RenderMode.ScreenSpaceOverlay && root.worldCamera != null)
                return root.worldCamera;
            bool cachedFrame = Application.isPlaying && frame == Time.frameCount;
            if (!cachedFrame) Refresh();
            if (root != null && Results.TryGetValue(root, out var cached) && cached != null)
            {
                if (cached.isActiveAndEnabled) return cached;
                // A camera can switch after another panel queried it earlier in this frame.
                // Re-scan immediately when the cached view is disabled.
                Refresh();
            }
            int display = root != null ? root.targetDisplay : 0;
            var main = Camera.main;
            Camera result = Eligible(main, display) ? main : null;
            if (result == null)
            {
                float best = float.NegativeInfinity;
                for (int i = 0; i < cameraCount; i++)
                {
                    var camera = cameras[i];
                    if (!Eligible(camera, display)) continue;
                    // Prefer a full view that clears the scene over small minimaps/UI-only views.
                    float coverage = camera.rect.width * camera.rect.height;
                    float score = coverage * 100000f + ((camera.clearFlags == CameraClearFlags.Skybox || camera.clearFlags == CameraClearFlags.SolidColor) ? 10000f : 0f) + Mathf.Clamp(camera.depth, -1000, 1000);
                    if (score > best) { best = score; result = camera; }
                }
            }
            if (root != null) Results[root] = result;
            return result;
        }
        private static bool Eligible(Camera camera, int display)
        {
            if (camera == null || !camera.isActiveAndEnabled || camera.cameraType != CameraType.Game || camera.targetTexture != null || camera.targetDisplay != display) return false;
            return !camera.TryGetComponent<UniversalAdditionalCameraData>(out var data) || data.renderType != CameraRenderType.Overlay;
        }
        private static void Refresh()
        {
            frame = Time.frameCount; Results.Clear();
            int required = Camera.allCamerasCount;
            if (cameras.Length < required) cameras = new Camera[Mathf.NextPowerOfTwo(required)];
            cameraCount = Camera.GetAllCameras(cameras);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { frame = -1; cameraCount = 0; Results.Clear(); System.Array.Clear(cameras, 0, cameras.Length); }
    }
}
