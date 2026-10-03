using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TranslucentUIFX
{
    public enum TranslucentUpdateMode
    {
        [InspectorName("⚡ Real-Time (Always)")] Always,
        [InspectorName("🧠 Smart Update (Camera Based)")] SmartUpdate,
        [InspectorName("📉 Performance (Interval)")] Interval,
        [InspectorName("🎯 Manual (Script Triggered)")] Manual
    }

    public enum TranslucentInjectionPoint
    {
        [InspectorName("✨ Auto (Recommended for URP 2D & 3D)")]
        Auto = 0,

        [InspectorName("Before Transparents (Camera & World Space UI)")]
        BeforeRenderingTransparents = RenderPassEvent.BeforeRenderingTransparents,
        
        [InspectorName("After Transparents (Overlay UI Default)")]
        AfterRenderingTransparents = RenderPassEvent.AfterRenderingTransparents,
        
        [InspectorName("After Post-Processing (Includes Final Effects)")]
        AfterRenderingPostProcessing = RenderPassEvent.AfterRenderingPostProcessing
    }

    public class TranslucentRendererFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class TranslucentSettings
        {
            [Header("Render Pipeline Routing")]
            [Tooltip("Auto intelligently matches URP 2D vs URP 3D constraints.")]
            public TranslucentInjectionPoint injectionPoint = TranslucentInjectionPoint.Auto;

            [Min(0), Tooltip("Match this layer number in a glass component or shared Capture Source.")]
            public int captureLayer;
            [Header("Capture quality")]
            [Range(0, 8)] public int backdropMipLevels = 4;
            [Range(0, 6)] public int blurPasses = 4;
            [Range(0.1f, 4f)] public float kernelSpread = 1f;
            [Range(0f, 2f)] public float dither = 0.5f;
            [Tooltip("Keep the camera color format, including HDR when enabled.")]
            public bool matchCameraFormat = true;
            public UnityEngine.Experimental.Rendering.GraphicsFormat captureFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;

            [Header("Internal Backend Configuration")]
            [Tooltip("Default fallback if no active instances specify a mode.")]
            public TranslucentUpdateMode updateMode = TranslucentUpdateMode.Always;
            [Tooltip("Default fallback if no active instances specify an interval.")]
            [Range(1, 120)] public int updateIntervalFrames = 3;
        }

        public TranslucentSettings settings = new TranslucentSettings();
        TranslucentRenderPass m_ScriptablePass;

        public static void RequestUpdate()
        {
            unchecked
            {
                s_UpdateRequestVersion++;
            }
        }

        public static string GlobalBlurTextureName = "_TranslucentUI_BlurredTex";
        public static int GlobalBlurTextureID => Shader.PropertyToID(GlobalBlurTextureName);

        private static int s_UpdateRequestVersion;

        internal static int UpdateRequestVersion => s_UpdateRequestVersion;

        internal static bool HasUpdateRequestSince(int processedVersion)
        {
            return processedVersion != s_UpdateRequestVersion;
        }

        private Material m_BlurMaterial;

        public override void Create()
        {
            if (m_BlurMaterial == null)
            {
                Shader shader = Shader.Find("Hidden/TranslucentUIFX/Blur");
                if (shader != null)
                    m_BlurMaterial = CoreUtils.CreateEngineMaterial(shader);
            }
            
            if (m_BlurMaterial != null)
            {
                m_ScriptablePass?.Dispose();
                m_ScriptablePass = new TranslucentRenderPass(settings, m_BlurMaterial);
            }
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // Preview, reflection and unsupported camera targets should never allocate persistent blur buffers.
            if (renderingData.cameraData.cameraType == CameraType.Preview ||
                renderingData.cameraData.cameraType == CameraType.Reflection ||
                renderingData.cameraData.camera == null)
                return;

#if UNITY_EDITOR
            // The editor-only camera callback supplies a deterministic translucent preview.
            // Do not allocate camera captures for Scene View because Overlay canvases are
            // composited outside the texture that powers the runtime optical effect.
            if (renderingData.cameraData.cameraType == CameraType.SceneView)
                return;
#endif

            // Only run if there are active UI elements requiring the blur and a valid material
            if (TranslucentImageFX.ActiveInstances.Count == 0 || m_BlurMaterial == null || m_ScriptablePass == null)
                return;

            // Do not force an intermediate camera texture when this view has no visible consumers.
            bool needed = false;
            foreach (var glass in TranslucentImageFX.ActiveInstances)
                if (glass != null && glass.NeedsBackground && glass.MatchesCapture(renderingData.cameraData.camera, settings.captureLayer)) { needed = true; break; }
            if (!needed) return;

            // Automatically resolve "Auto" injection point based on 2D vs 3D pipeline
            if (settings.injectionPoint == TranslucentInjectionPoint.Auto)
            {
                bool isURP2D = renderer.GetType().Name.Contains("Renderer2D");
                // 3D works flawlessly at AfterRenderingTransparents.
                // 2D inherently skips Transparents, so it must use BeforeRenderingPostProcessing 
                // to grab the texture before it gets blitted to the unreadable backbuffer.
                m_ScriptablePass.renderPassEvent = isURP2D ? RenderPassEvent.BeforeRenderingPostProcessing : RenderPassEvent.AfterRenderingTransparents;
            }
            else
            {
                m_ScriptablePass.renderPassEvent = (RenderPassEvent)settings.injectionPoint;
            }

            renderer.EnqueuePass(m_ScriptablePass);
        }
        
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (m_ScriptablePass != null)
                m_ScriptablePass.Dispose();
                
            CoreUtils.Destroy(m_BlurMaterial);
        }
    }
}
