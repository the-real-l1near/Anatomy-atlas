using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TranslucentUIFX.Editor
{
    /// <summary>
    /// Selects a deterministic shader preview before each editor camera renders. Scene View
    /// cannot consume the Game-camera capture reliably for Screen Space Overlay canvases, while
    /// Game View must continue to use the complete runtime optical path.
    /// </summary>
    [InitializeOnLoad]
    internal static class TranslucentSceneViewPreview
    {
        private static readonly int SceneViewPreviewId = Shader.PropertyToID("_TranslucentUI_SceneViewPreview");

        static TranslucentSceneViewPreview()
        {
            Shader.SetGlobalFloat(SceneViewPreviewId, 0f);
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
        }

        private static void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            bool stablePreview = camera != null && camera.cameraType == CameraType.SceneView;
            Shader.SetGlobalFloat(SceneViewPreviewId, stablePreview ? 1f : 0f);
        }

        private static void HandleBeforeAssemblyReload()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            Shader.SetGlobalFloat(SceneViewPreviewId, 0f);
        }
    }
}
