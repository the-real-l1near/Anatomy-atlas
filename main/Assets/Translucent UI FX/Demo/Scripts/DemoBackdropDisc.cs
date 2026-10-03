using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TranslucentUIFX.Demo
{
    /// <summary>
    /// Supplies a generated circle sprite to a demo Image without relying on an imported
    /// sprite. Keeping this procedural makes the scene resilient to sprite slicing changes.
    /// </summary>
    [ExecuteAlways]
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class DemoBackdropDisc : MonoBehaviour
    {
        private const int TextureSize = 128;
        private static Sprite sharedSprite;

        [SerializeField] private bool m_VisibleInCanvas = true;

        public bool VisibleInCanvas
        {
            get => m_VisibleInCanvas;
            set
            {
                if (m_VisibleInCanvas == value)
                    return;

                m_VisibleInCanvas = value;
                ApplySprite();
            }
        }

        private void OnEnable()
        {
            ApplySprite();
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            ApplySprite();
        }

        private void ApplySprite()
        {
            Image image = GetComponent<Image>();
            if (image == null)
                return;

            image.sprite = GetSharedSprite();
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.canvasRenderer.SetAlpha(m_VisibleInCanvas ? 1f : 0f);
            image.SetVerticesDirty();
        }

        private static Sprite GetSharedSprite()
        {
            if (sharedSprite != null)
                return sharedSprite;

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "Fused Demo Backdrop Disc",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[TextureSize * TextureSize];
            float center = (TextureSize - 1) * 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 1f - distance) * 255f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sharedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                TextureSize,
                0,
                SpriteMeshType.FullRect);
            sharedSprite.name = "Fused Demo Backdrop Disc";
            sharedSprite.hideFlags = HideFlags.HideAndDontSave;
            return sharedSprite;
        }
    }
}
