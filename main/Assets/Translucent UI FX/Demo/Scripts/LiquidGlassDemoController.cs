using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TranslucentUIFX.Demo
{
    [ExecuteAlways, AddComponentMenu("")]
    public sealed class LiquidGlassDemoController : MonoBehaviour
    {
        [SerializeField] private TranslucentImageFX target;
        [SerializeField] private LiquidGlassDemoBackdrop backdrop;
        [SerializeField] private Button resetButton;
        [SerializeField, HideInInspector] private Button shapeButton; // Existing demo compatibility.
        [SerializeField] private Button[] shapeButtons;
        [SerializeField] private TranslucentImageFX shapePreview;
        [SerializeField] private TranslucentGlassGroup fusion;
        [SerializeField] private TranslucentGlassMember cutout;
        [SerializeField] private TranslucentGlassMember fusionSatellite;
        [SerializeField] private Slider blurSlider, refractionSlider, radiusSlider, fusionSlider;
        [SerializeField] private Toggle pointerToggle, motionToggle, cutoutToggle;
        [SerializeField] private TMP_Text blurValueLabel, refractionValueLabel, radiusValueLabel, fusionValueLabel;
        private UnityAction[] shapeActions;
        private Vector3[] initialPositions;
        private Sprite lastSprite;
        private Sprite buttonSprite;
        private static readonly Color Ink = new Color(0.035f, 0.075f, 0.12f);
        private static readonly Color Accent = new Color(0.64f, 0.88f, 1f);
        private static readonly Color Muted = new Color(0.66f, 0.76f, 0.86f);
        private static readonly Color ButtonIdle = new Color(0.09f, 0.16f, 0.25f, 0.88f);
        private EdgeShape lastShape = (EdgeShape)(-1);
        public static readonly EdgeShape[] Shapes = { EdgeShape.ContinuousRoundedRect, EdgeShape.RoundedRect, EdgeShape.Capsule, EdgeShape.Circle, EdgeShape.Rectangle, EdgeShape.Diamond, EdgeShape.Triangle, EdgeShape.Hexagon };
        private static readonly string[] ShapeNames = { "Continuous", "Rounded", "Capsule", "Circle", "Rectangle", "Diamond", "Triangle", "Hexagon" };

        private void OnEnable()
        {
            ApplyDemoTypography();
            // The playground demonstrates a round satellite and an optional circular hole.
            if (fusionSatellite != null) fusionSatellite.Shape = EdgeShape.Circle;
            if (cutout != null) { cutout.Shape = EdgeShape.Circle; cutout.Operation = GlassOperation.Cutout; cutout.Include = false; }
            ConfigureSlider(blurSlider, 1f);
            ConfigureSlider(refractionSlider, 0.12f);
            ConfigureSlider(radiusSlider, 0.5f);
            ConfigureSlider(fusionSlider, 96f);
            if (fusion != null)
            {
                fusion.RefreshMembers();
                initialPositions = new Vector3[fusion.Members.Count];
                for (int i = 0; i < initialPositions.Length; i++) initialPositions[i] = fusion.Members[i].transform.localPosition;
            }
            SyncControls();
            if (resetButton != null) resetButton.onClick.AddListener(ResetGlass);
            if (shapeButton != null) shapeButton.onClick.AddListener(CycleShape);
            if (blurSlider != null) blurSlider.onValueChanged.AddListener(SetBlur);
            if (refractionSlider != null) refractionSlider.onValueChanged.AddListener(SetRefraction);
            if (radiusSlider != null) radiusSlider.onValueChanged.AddListener(SetRadius);
            if (fusionSlider != null) fusionSlider.onValueChanged.AddListener(SetFusion);
            if (pointerToggle != null) pointerToggle.onValueChanged.AddListener(SetPointerLighting);
            if (motionToggle != null) motionToggle.onValueChanged.AddListener(SetMotion);
            if (cutoutToggle != null) cutoutToggle.onValueChanged.AddListener(SetCutout);
            if (shapeButtons != null)
            {
                shapeActions = new UnityAction[shapeButtons.Length];
                for (int i = 0; i < shapeButtons.Length && i < Shapes.Length; i++)
                {
                    EdgeShape shape = Shapes[i];
                    shapeActions[i] = () => SetShape(shape);
                    if (shapeButtons[i] != null) shapeButtons[i].onClick.AddListener(shapeActions[i]);
                }
            }
        }

        private void OnDisable()
        {
            if (buttonSprite != null)
            {
                if (target != null && target.canvas != null)
                    foreach (Image image in target.canvas.GetComponentsInChildren<Image>(true))
                        if (image.sprite == buttonSprite) image.sprite = null;
                if (Application.isPlaying) Destroy(buttonSprite); else DestroyImmediate(buttonSprite);
                buttonSprite = null;
            }
            if (resetButton != null) resetButton.onClick.RemoveListener(ResetGlass);
            if (shapeButton != null) shapeButton.onClick.RemoveListener(CycleShape);
            if (blurSlider != null) blurSlider.onValueChanged.RemoveListener(SetBlur);
            if (refractionSlider != null) refractionSlider.onValueChanged.RemoveListener(SetRefraction);
            if (radiusSlider != null) radiusSlider.onValueChanged.RemoveListener(SetRadius);
            if (fusionSlider != null) fusionSlider.onValueChanged.RemoveListener(SetFusion);
            if (pointerToggle != null) pointerToggle.onValueChanged.RemoveListener(SetPointerLighting);
            if (motionToggle != null) motionToggle.onValueChanged.RemoveListener(SetMotion);
            if (cutoutToggle != null) cutoutToggle.onValueChanged.RemoveListener(SetCutout);
            if (shapeButtons != null && shapeActions != null)
                for (int i = 0; i < shapeButtons.Length && i < shapeActions.Length; i++)
                    if (shapeButtons[i] != null && shapeActions[i] != null) shapeButtons[i].onClick.RemoveListener(shapeActions[i]);
        }

        // Reflect inspector edits without emitting callbacks or resetting the authored value.
        private void LateUpdate() { SyncControls(); }

        private void ApplyDemoTypography()
        {
            if (target == null || target.canvas == null) return;
            if (target.canvas.TryGetComponent(out CanvasScaler scaler))
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Inter-Regular SDF");
            if (font == null) return;
            foreach (TMP_Text label in target.canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = font;
                label.fontSharedMaterial = font.material;
                label.color = new Color(0.94f, 0.97f, 1f);
                if (label.name == "Subtitle" || label.name == "Footer" || label.name == "DragHint" || label.name == "Edition") label.color = Muted;
                if (label.name == "ShapeHeading" || label.name == "FusionHeading" || label.name == "Brand")
                { label.color = Accent; label.characterSpacing = 2.5f; }
                if (label.name == "Title") label.characterSpacing = -2f;
                label.raycastTarget = false;
            }
            Texture2D buttonTexture = Resources.Load<Texture2D>("DemoButton");
            if (buttonTexture != null && buttonSprite == null)
            {
                buttonSprite = Sprite.Create(buttonTexture, new Rect(0, 0, buttonTexture.width, buttonTexture.height), Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect, Vector4.one * 32f);
                buttonSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            foreach (Button button in target.canvas.GetComponentsInChildren<Button>(true))
            {
                ColorBlock colors = button.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.83f, 0.93f, 1f);
                colors.pressedColor = new Color(0.65f, 0.81f, 0.95f);
                colors.selectedColor = Color.white;
                button.colors = colors;
                if (button.image != null)
                {
                    button.image.color = ButtonIdle;
                    RoundControl(button.image);
                }
            }
            foreach (Toggle toggle in target.canvas.GetComponentsInChildren<Toggle>(true))
            {
                ColorBlock colors = toggle.colors;
                colors.normalColor = Color.white;
                colors.highlightedColor = new Color(0.83f, 0.93f, 1f);
                colors.pressedColor = new Color(0.65f, 0.81f, 0.95f);
                colors.selectedColor = Color.white;
                toggle.colors = colors;
                if (toggle.targetGraphic is Image track) RoundControl(track);
                if (toggle.graphic is Image knob) { RoundControl(knob); knob.color = Ink; }
                StyleToggle(toggle);
            }
            lastShape = (EdgeShape)(-1);
        }

        private void RoundControl(Image image)
        {
            if (buttonSprite == null) return;
            image.sprite = buttonSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 3f;
        }

        private static void StyleToggle(Toggle toggle)
        {
            if (toggle == null || toggle.targetGraphic == null) return;
            toggle.targetGraphic.color = toggle.isOn ? Accent : new Color(0.16f, 0.25f, 0.35f, 1f);
            if (toggle.graphic == null) return;

            // A switch keeps its thumb in both states. Cancel Toggle's checkbox fade
            // after state synchronization, including SetIsOnWithoutNotify and reset.
            Graphic thumb = toggle.graphic;
            thumb.CrossFadeAlpha(1f, 0f, true);
            thumb.color = toggle.isOn ? Ink : new Color(0.85f, 0.92f, 0.98f);
            RectTransform track = toggle.targetGraphic.rectTransform;
            RectTransform handle = thumb.rectTransform;
            float diameter = Mathf.Max(0f, track.rect.height - 6f);
            float travel = Mathf.Max(0f, (track.rect.width - diameter) * 0.5f - 3f);
            handle.anchorMin = handle.anchorMax = handle.pivot = Vector2.one * 0.5f;
            handle.sizeDelta = Vector2.one * diameter;
            handle.anchoredPosition = new Vector2(toggle.isOn ? travel : -travel, 0f);
        }

        public void SetShape(EdgeShape shape)
        {
            if (shapePreview == null) return;
            shapePreview.overrideSprite = null;
            shapePreview.sprite = null;
            shapePreview.EdgeShape = shape;
            shapePreview.ProceduralShape = true;
            shapePreview.SetAllDirty();
            TranslucentRendererFeature.RequestUpdate();
            SyncControls();
        }
        private void CycleShape()
        {
            if (shapePreview == null) return;
            int index = System.Array.IndexOf(Shapes, shapePreview.EdgeShape);
            SetShape(Shapes[(index + 1) % Shapes.Length]);
        }
        private void ResetGlass()
        {
            if (target == null) return;
            target.ApplyLiquidGlass();
            if (fusion != null)
            {
                fusion.ApplyLiquidGlass();
                fusion.FusionSoftness = 28f;
                if (initialPositions != null)
                    for (int i = 0; i < initialPositions.Length && i < fusion.Members.Count; i++) fusion.Members[i].transform.localPosition = initialPositions[i];
            }
            SetCutout(false);
            SyncControls();
        }
        private void SetBlur(float value) { if (target == null) return; target.BlurStrength = value; if (fusion != null) fusion.BlurStrength = value; RefreshEffect(); }
        private void SetRefraction(float value) { if (target == null) return; target.RefractionAmount = value; if (fusion != null) fusion.RefractionAmount = value; RefreshEffect(); }
        private void SetRadius(float value) { if (shapePreview == null) return; shapePreview.EdgeRounding = value; shapePreview.IndividualCorners = false; shapePreview.SetAllDirty(); SyncControls(); }
        private void SetFusion(float value) { if (fusion != null) fusion.FusionSoftness = value; SyncControls(); }
        private void SetCutout(bool value) { if (cutout != null) cutout.Include = value; StyleToggle(cutoutToggle); }
        private void SetPointerLighting(bool value) { if (target == null) return; target.InteractiveGlare = value; RefreshEffect(); }
        private void SetMotion(bool value) { if (backdrop != null) backdrop.Animate = value; StyleToggle(motionToggle); }
        private static void ConfigureSlider(Slider slider, float maximum)
        {
            if (slider == null) return;
            slider.minValue = 0f; slider.maxValue = maximum; slider.wholeNumbers = false;
        }
        private void SyncControls()
        {
            if (target == null) return;
            if (blurSlider != null) blurSlider.SetValueWithoutNotify(target.BlurStrength);
            if (refractionSlider != null) refractionSlider.SetValueWithoutNotify(target.RefractionAmount);
            if (pointerToggle != null) pointerToggle.SetIsOnWithoutNotify(target.InteractiveGlare);
            if (motionToggle != null && backdrop != null) motionToggle.SetIsOnWithoutNotify(backdrop.Animate);
            if (cutoutToggle != null && cutout != null) cutoutToggle.SetIsOnWithoutNotify(cutout.Include);
            StyleToggle(pointerToggle); StyleToggle(motionToggle); StyleToggle(cutoutToggle);
            if (fusionSlider != null && fusion != null) fusionSlider.SetValueWithoutNotify(fusion.FusionSoftness);
            if (blurValueLabel != null) blurValueLabel.text = target.BlurStrength.ToString("0.00");
            if (refractionValueLabel != null) refractionValueLabel.text = target.RefractionAmount.ToString("0.000");
            if (fusionValueLabel != null && fusion != null) fusionValueLabel.text = fusion.FusionSoftness.ToString("0");
            if (shapePreview == null) return;
            if (radiusSlider != null)
            {
                radiusSlider.SetValueWithoutNotify(shapePreview.EdgeRounding);
                radiusSlider.interactable = !shapePreview.UsesSpriteSilhouette && (shapePreview.EdgeShape == EdgeShape.RoundedRect || shapePreview.EdgeShape == EdgeShape.ContinuousRoundedRect);
            }
            if (radiusValueLabel != null) radiusValueLabel.text = radiusSlider != null && !radiusSlider.interactable ? "—" : shapePreview.EdgeRounding.ToString("0.00");
            if (lastShape == shapePreview.EdgeShape && lastSprite == shapePreview.SilhouetteSprite) return;
            lastSprite = shapePreview.SilhouetteSprite;
            lastShape = shapePreview.EdgeShape;
            int selected = shapePreview.UsesSpriteSilhouette ? -1 : System.Array.IndexOf(Shapes, lastShape);
            if (shapeButton != null)
            {
                TMP_Text label = shapeButton.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = selected < 0 ? "Shape" : ShapeNames[selected] + "  >";
            }
            if (shapeButtons == null) return;
            for (int i = 0; i < shapeButtons.Length; i++)
            {
                Button button = shapeButtons[i]; if (button == null) continue;
                if (button.image != null) button.image.color = i == selected ? Accent : ButtonIdle;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>();
                if (label != null) label.color = i == selected ? Ink : new Color(0.86f, 0.93f, 1f);
            }
        }
        private void RefreshEffect()
        {
            target.SetAllDirty(); if (fusion != null) fusion.SetAllDirty();
            TranslucentRendererFeature.RequestUpdate(); SyncControls();
        }
    }
}
