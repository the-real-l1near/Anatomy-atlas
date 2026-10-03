using UnityEngine;

namespace TranslucentUIFX
{
    /// <summary>Optional shared camera/layer routing for a Canvas or UI subtree.</summary>
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("UI/Liquid Glass Capture Source")]
    public sealed class GlassCaptureSource : MonoBehaviour
    {
        public Camera Camera;
        [Min(0)] public int Layer;
        private void OnEnable() => Refresh();
        private void OnDisable() => Refresh();
#if UNITY_EDITOR
        private void OnValidate() { Layer = Mathf.Max(0, Layer); Refresh(); }
#endif
        private void Refresh()
        {
            foreach (var glass in GetComponentsInChildren<TranslucentImageFX>(true)) glass.InvalidateCaptureSource();
            TranslucentRendererFeature.RequestUpdate();
        }
    }
}
