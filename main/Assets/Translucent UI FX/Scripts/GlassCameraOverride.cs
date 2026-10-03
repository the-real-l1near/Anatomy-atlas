using UnityEngine;

namespace TranslucentUIFX
{
    [ExecuteAlways, RequireComponent(typeof(Camera)), DisallowMultipleComponent, AddComponentMenu("Rendering/Liquid Glass Camera Override")]
    public sealed class GlassCameraOverride : MonoBehaviour
    {
        public bool OverrideQuality;
        public PerformanceMode Quality = PerformanceMode.High;
        public bool OverrideBlur;
        [Range(0, 6)] public int BlurPasses = 4;
        [Range(0.1f, 4f)] public float KernelSpread = 1f;
        [Range(0f, 2f)] public float Dither = 0.5f;
        private void OnEnable() => TranslucentRendererFeature.RequestUpdate();
        private void OnDisable() => TranslucentRendererFeature.RequestUpdate();
#if UNITY_EDITOR
        private void OnValidate() => TranslucentRendererFeature.RequestUpdate();
#endif
    }
}
