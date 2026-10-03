using UnityEngine;
using UnityEngine.UI;

namespace TranslucentUIFX.Demo
{
    /// <summary>Styles and wires the authored slider children without generating hidden UI.</summary>
    [ExecuteAlways, RequireComponent(typeof(Slider)), AddComponentMenu("")]
    public sealed class LiquidGlassDemoSlider : MonoBehaviour
    {
        private Texture2D thumbTexture;
        private Sprite thumbSprite;
        private void OnEnable()
        {
            Slider slider = GetComponent<Slider>();
            Image background = transform.Find("Background")?.GetComponent<Image>();
            if (background != null) { background.color = Color.clear; background.raycastTarget = true; }
            RectTransform fillArea = transform.Find("Fill Area") as RectTransform;
            if (fillArea != null) SetTrack(fillArea);
            RectTransform track = transform.Find("Track") as RectTransform;
            if (track != null) { SetTrack(track); Image rail = track.GetComponent<Image>(); rail.raycastTarget = false; rail.color = new Color(1f, 1f, 1f, 0.24f); }
            RectTransform area = transform.Find("Handle Area") as RectTransform;
            RectTransform handle = transform.Find("Handle Area/Handle") as RectTransform;
            if (area == null || handle == null) return;
            area.anchorMin = new Vector2(0f, 0.5f); area.anchorMax = new Vector2(1f, 0.5f);
            area.offsetMin = new Vector2(12f, -12f); area.offsetMax = new Vector2(-12f, 12f);
            handle.sizeDelta = new Vector2(24f, 0f);
            handle.anchoredPosition = Vector2.zero;
            if (area.TryGetComponent(out Image hit)) hit.raycastTarget = false;
            Image image = handle.GetComponent<Image>();
            image.color = Color.white;
            thumbTexture = new Texture2D(48, 48, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear };
            Color[] pixels = new Color[48 * 48];
            for (int y = 0; y < 48; y++) for (int x = 0; x < 48; x++)
            {
                float distance = new Vector2(x - 23.5f, y - 23.5f).magnitude;
                pixels[y * 48 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(23f - distance));
            }
            thumbTexture.SetPixels(pixels); thumbTexture.Apply(false, true);
            thumbSprite = Sprite.Create(thumbTexture, new Rect(0, 0, 48, 48), Vector2.one * 0.5f);
            thumbSprite.hideFlags = HideFlags.HideAndDontSave;
            image.sprite = thumbSprite;
            image.raycastTarget = true;
            slider.handleRect = handle;
            slider.targetGraphic = image;
            if (slider.fillRect != null && slider.fillRect.TryGetComponent(out Image fill))
            {
                fill.color = new Color(0.57f, 0.82f, 1f);
                fill.canvasRenderer.SetColor(Color.white);
                fill.raycastTarget = false;
            }
            ColorBlock colors = slider.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.84f, 0.93f, 1f);
            colors.pressedColor = new Color(0.5f, 0.78f, 1f);
            colors.selectedColor = new Color(0.84f, 0.93f, 1f);
            slider.colors = colors;
        }
        private void OnDisable()
        {
            Image handle = transform.Find("Handle Area/Handle")?.GetComponent<Image>();
            if (handle != null && handle.sprite == thumbSprite) handle.sprite = null;
            if (Application.isPlaying) { Destroy(thumbSprite); Destroy(thumbTexture); }
            else { DestroyImmediate(thumbSprite); DestroyImmediate(thumbTexture); }
        }
        private static void SetTrack(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 0.5f); rect.anchorMax = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(12f, -2f); rect.offsetMax = new Vector2(-12f, 2f);
        }
    }
}
