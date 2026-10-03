using UnityEngine;
using UnityEngine.Rendering;

namespace TranslucentUIFX.Demo
{
    /// <summary>An original artwork backdrop rendered by the camera, so glass can refract it.</summary>
    [ExecuteAlways, RequireComponent(typeof(Camera)), AddComponentMenu("")]
    public sealed class LiquidGlassDemoBackdrop : MonoBehaviour
    {
        [SerializeField] private Shader backdropShader;
        [SerializeField] private bool animate = true;
        private Camera owner;
        private GameObject surface;
        private Material surfaceMaterial;
        private Mesh surfaceMesh;
        private float elapsed;
        private static readonly int ArtworkId = Shader.PropertyToID("_Artwork");
        private static readonly int ArtworkScaleId = Shader.PropertyToID("_ArtworkScale");
        private static readonly int TimeId = Shader.PropertyToID("_DemoTime");

        public bool Animate { get => animate; set => animate = value; }

        private void OnEnable()
        {
            owner = GetComponent<Camera>();
            if (backdropShader == null) backdropShader = Shader.Find("TranslucentUIFX/Demo/Clarity Backdrop");
            if (backdropShader == null) return;
            surfaceMaterial = new Material(backdropShader) { hideFlags = HideFlags.HideAndDontSave };
            Texture2D artwork = Resources.Load<Texture2D>("SatinBackdrop");
            if (artwork != null) surfaceMaterial.SetTexture(ArtworkId, artwork);
            surfaceMaterial.SetFloat("_HasArtwork", artwork != null ? 1f : 0f);
            surfaceMesh = new Mesh { name = "Clarity Backdrop Quad", hideFlags = HideFlags.HideAndDontSave };
            surfaceMesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
            surfaceMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            surfaceMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            surfaceMesh.RecalculateBounds();
            surface = new GameObject("Clarity Backdrop (generated)", typeof(MeshFilter), typeof(MeshRenderer));
            surface.hideFlags = HideFlags.HideAndDontSave;
            surface.transform.SetParent(transform, false);
            surface.GetComponent<MeshFilter>().sharedMesh = surfaceMesh;
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaceMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            UpdateSurface();
        }

        private void Update()
        {
            if (Application.isPlaying && animate) elapsed += Time.unscaledDeltaTime;
            if (surfaceMaterial != null) surfaceMaterial.SetFloat(TimeId, elapsed);
            UpdateSurface();
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == owner) UpdateSurface();
        }

        private void UpdateSurface()
        {
            if (surface == null || owner == null) return;
            // Cover the viewport without stretching the source artwork.
            const float artworkAspect = 1672f / 941f;
            float aspect = Mathf.Max(0.01f, owner.aspect);
            Vector2 crop = aspect > artworkAspect ? new Vector2(1f, artworkAspect / aspect) : new Vector2(aspect / artworkAspect, 1f);
            surfaceMaterial.SetVector(ArtworkScaleId, new Vector4(crop.x, crop.y, 0f, 0f));
            float distance = owner.nearClipPlane + 1f;
            float height = owner.orthographic ? owner.orthographicSize * 2f : 2f * distance * Mathf.Tan(owner.fieldOfView * 0.5f * Mathf.Deg2Rad);
            surface.transform.localPosition = new Vector3(0f, 0f, distance);
            surface.transform.localScale = new Vector3(height * owner.aspect * 1.01f, height * 1.01f, 1f);
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            Release(surface);
            Release(surfaceMaterial);
            Release(surfaceMesh);
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
