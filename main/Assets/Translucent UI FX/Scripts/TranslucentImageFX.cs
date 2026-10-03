using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TranslucentUIFX
{
    public enum GlassFieldResolution { Low = 64, Medium = 128, Standard = 256, High = 512, Ultra = 1024 }

    public enum PerformanceMode
    {
        Low,
        Medium,
        High
    }

    // Numeric values are retained for existing scenes and scripts. New authoring uses Liquid Glass only.
    public enum GlassPreset
    {
        Custom,
        DefaultGlass,
        SoftFrost,
        DarkGlass,
        WhiteGlass,
        LiquidGlass,
        StrongBlur
    }

    public enum EdgeShape
    {
        Rectangle,
        Circle,
        RoundedRect,
        Diamond,
        Capsule,
        ContinuousRoundedRect,
        Triangle,
        Hexagon
    }

    [AddComponentMenu("UI/Liquid Glass")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class TranslucentImageFX : Image, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [Header("Translucent FX Settings")]
        [HideInInspector] public GlassPreset CurrentPreset = GlassPreset.LiquidGlass;
        [HideInInspector] public bool AdvancedMode = false;
        [Range(0f, 1f)] public float GlassIntensity = 1f;
        [Range(0f, 1f)] public float LuminosityBoost = 0f;
        
        [Range(0f, 3f)] public float Brightness = 1.0f;
        [Range(0f, 3f)] public float Saturation = 1.0f;
        [Range(0f, 3f)] public float Contrast = 1.0f;
        
        public bool AutoReadability = false;
        
        [Range(0f, 1f)] public float BlurStrength = 0.08f;
        public Color TintColor = new Color(1f, 1f, 1f, 0.035f);
        [Range(0f, 1f)] public float FrostAmount = 0f;
        [Range(0f, 1f)] public float NoiseAmount = 0f;
        
        [Header("Optical Glass Settings")]
        [Range(-0.2f, 0.2f)] public float RefractionAmount = 0.035f;
        [Range(1f, 2.5f)] public float RefractiveIndex = 1.46f;
        public bool SphericalDistortion = true;
        [Range(0f, 0.1f)] public float ChromaticAberration = 0.0015f;
        [Tooltip("Fades refraction and color dispersion with the sprite or CanvasGroup alpha. Keep enabled for soft masks and alpha gradients to prevent doubled edges and shimmer over moving backgrounds.")]
        public bool StabilizeAlphaGradients = true;
        [Tooltip("Master switch for the bright specular flare. Disable it to keep refraction and edge lighting without the highlight.")]
        public bool EnableSpecularGlare = true;
        [Range(0f, 1f)] public float SpecularGlare = 0.18f;
        [Range(8f, 256f)] public float SpecularSharpness = 160f;
        [Range(0f, 1f)] public float RimDepth = 0.18f;

        [Header("Interactive Lighting")]
        public bool InteractiveGlare = false;
        public Vector2 LightDirection = new Vector2(-0.5f, 0.55f);
        [Range(1f, 30f)] public float LightFollowSpeed = 12f;
        
        public bool EnableEdgeLighting = true;
        public EdgeShape EdgeShape = EdgeShape.ContinuousRoundedRect;
        [Tooltip("Generates the visible silhouette analytically, so rounded rectangles, pills and circles need no sprite.")]
        public bool ProceduralShape = true;
        [Range(0f, 0.5f)] public float EdgeRounding = 0.18f;
        [Range(0f, 1f)] public float CornerContinuity = 0.82f;
        public bool IndividualCorners;
        public Vector4 CornerRadii = new Vector4(0.18f, 0.18f, 0.18f, 0.18f);
        public Color EdgeLightColor = new Color(1f, 1f, 1f, 0.48f);
        [Range(0f, 1f)] public float EdgeLightWidth = 0.012f;
        [Range(0.1f, 10f)] public float EdgeLightPower = 2.5f;

        [Header("Performance & Quality")]
        public PerformanceMode QualityMode = PerformanceMode.High;
        public TranslucentUpdateMode UpdateMode = TranslucentUpdateMode.Always;
        [Range(1, 120)] public int UpdateInterval = 3;
        
        // Keep track of active instances so the renderer feature can read them.
        [Tooltip("Optional advanced override. Leave empty for automatic background selection.")]
        public Camera CaptureCamera;
        private GlassCaptureSource m_SharedCaptureSource;
        private bool m_CaptureSourceKnown;
        private Texture m_BoundSource, m_BoundBlur;
        private float m_BoundMaxBlur = -1f, m_BoundDither = -1f;
        private bool m_WasCaptureBound;
        private GlassCaptureSource SharedCaptureSource
        {
            get
            {
                if (!m_CaptureSourceKnown) { m_SharedCaptureSource = GetComponentInParent<GlassCaptureSource>(); m_CaptureSourceKnown = true; }
                return m_SharedCaptureSource;
            }
        }
        internal void InvalidateCaptureSource() { m_CaptureSourceKnown = false; SetMaterialDirty(); }
        public bool HasBackgroundImage => GlassCaptureRegistry.TryGet(ResolvedCaptureCamera, ResolvedCaptureLayer, out var capture) && capture.Source != null && capture.Blur != null;
        internal bool NeedsBackground => isActiveAndEnabled && GlassIntensity > 0f && color.a > 0f && canvasRenderer.GetInheritedAlpha() > 0f;
        [Min(0)] public int CaptureLayer;
        public Camera ResolvedCaptureCamera
        {
            get
            {
                if (CaptureCamera != null) return CaptureCamera;
                var shared = SharedCaptureSource;
                if (shared != null && shared.isActiveAndEnabled && shared.Camera != null) return shared.Camera;
                return GlassCameraResolver.Resolve(canvas);
            }
        }
        public int ResolvedCaptureLayer
        {
            get
            {
                if (CaptureCamera != null) return Mathf.Max(0, CaptureLayer);
                var shared = SharedCaptureSource;
                return shared != null && shared.isActiveAndEnabled ? Mathf.Max(0, shared.Layer) : Mathf.Max(0, CaptureLayer);
            }
        }
        internal bool MatchesCapture(Camera camera, int layer) => ResolvedCaptureCamera == camera && ResolvedCaptureLayer == layer;
        internal void RefreshCaptureBinding(bool force = false)
        {
            if (m_CustomMaterial == null) return;
            bool bound = GlassCaptureRegistry.TryGet(ResolvedCaptureCamera, ResolvedCaptureLayer, out var capture) && capture.Source != null && capture.Blur != null;
            if (!force && bound == m_WasCaptureBound && (!bound || (m_BoundSource == capture.Source && m_BoundBlur == capture.Blur && m_BoundMaxBlur == capture.MaxBlur && m_BoundDither == capture.Dither))) return;
            m_WasCaptureBound = bound;
            m_BoundSource = bound ? capture.Source : null; m_BoundBlur = bound ? capture.Blur : null;
            m_BoundMaxBlur = bound ? capture.MaxBlur : -1f; m_BoundDither = bound ? capture.Dither : -1f;
            m_CustomMaterial.SetFloat("_CaptureUnavailable", bound ? 0f : 1f);
            m_CustomMaterial.SetFloat("_CaptureBound", bound ? 1f : 0f);
            if (!bound) return;
            m_CustomMaterial.SetTexture("_GlassSourceTex", capture.Source); m_CustomMaterial.SetTexture("_GlassBlurredTex", capture.Blur);
            m_CustomMaterial.SetVector("_CaptureOptions", new Vector4(capture.MaxBlur, capture.Dither, 0, 0));
        }

        public GlassCornerSettings CornerSettings = new GlassCornerSettings();
        public GlassSurfaceSettings Surface = new GlassSurfaceSettings();
        public virtual float RequiredBlurStrength => BlurStrength;

        public static readonly List<TranslucentImageFX> ActiveInstances = new List<TranslucentImageFX>();

        [Range(-32f, 64f)] public float SilhouetteRaycastPadding;
        private readonly List<UIVertex> m_HitVertices = new List<UIVertex>();
        private readonly List<UIVertex> m_ShadowVertices = new List<UIVertex>();
        private Material m_CustomMaterial;
        [Range(0.01f, 0.99f)] public float SpriteAlphaThreshold = 0.5f;
        public GlassFieldResolution SpriteFieldResolution = GlassFieldResolution.Standard;
        private GlassSpriteField.Entry m_SpriteField;
        public bool UsesSpriteSilhouette => this is not TranslucentGlassGroup && overrideSprite != null;
        public Sprite SilhouetteSprite => UsesSpriteSilhouette ? overrideSprite : null;

        private void RefreshSpriteField(bool dirty = false)
        {
            Sprite requested = SilhouetteSprite;
            if (m_SpriteField != null && m_SpriteField.Matches(requested, SpriteAlphaThreshold, (int)SpriteFieldResolution)) return;
            if (m_SpriteField == null && requested == null) return;
            GlassSpriteField.Release(m_SpriteField);
            m_SpriteField = GlassSpriteField.Acquire(requested, SpriteAlphaThreshold, (int)SpriteFieldResolution);
            if (dirty) SetMaterialDirty();
        }
        private Vector2 m_CurrentLightDirection;
        private Vector2 m_TargetLightDirection;
        private bool m_PointerInside;

        /// <summary>Restores the liquid glass appearance without changing shape, sprite, or capture settings.</summary>
        public void ApplyLiquidGlass()
        {
            Surface = new GlassSurfaceSettings();
            CurrentPreset = GlassPreset.LiquidGlass;
            GlassIntensity = 1f;
            LuminosityBoost = 0f;
            Brightness = Saturation = Contrast = 1f;
            AutoReadability = false;
            TintColor = new Color(1f, 1f, 1f, 0.035f);
            FrostAmount = NoiseAmount = 0f;
            BlurStrength = 0.08f;
            EnableEdgeLighting = true;
            EdgeLightColor = new Color(1f, 1f, 1f, 0.48f);
            EdgeLightWidth = 0.012f;
            EdgeLightPower = 2.5f;
            RefractionAmount = 0.035f;
            RefractiveIndex = 1.46f;
            SphericalDistortion = true;
            ChromaticAberration = 0.0015f;
            StabilizeAlphaGradients = true;
            EnableSpecularGlare = true;
            SpecularGlare = 0.18f;
            SpecularSharpness = 160f;
            RimDepth = 0.18f;
            InteractiveGlare = false;
            LightDirection = new Vector2(-0.5f, 0.55f);
            LightFollowSpeed = 12f;
            ResetInteractiveLight();
            SetMaterialDirty();
            TranslucentRendererFeature.RequestUpdate();
        }

        /// <summary>Compatibility entry point. Retired styles now use Liquid Glass; Custom leaves values intact.</summary>
        public void ApplyPreset(GlassPreset preset)
        {
            if (preset == GlassPreset.Custom)
            {
                CurrentPreset = preset;
                return;
            }
            ApplyLiquidGlass();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            m_CaptureSourceKnown = false;
            if (!ActiveInstances.Contains(this)) ActiveInstances.Add(this);
            EnsureCanvasChannels();
            ResetInteractiveLight();
            SetMaterialDirty();
            TranslucentRendererFeature.RequestUpdate();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            GlassSpriteField.Release(m_SpriteField); m_SpriteField = null;
            ActiveInstances.Remove(this);
            TranslucentRendererFeature.RequestUpdate();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            CornerRadii = GlassShapeUtility.ClampCorners(CornerRadii);
            LightDirection = ClampLightDirection(LightDirection);
            if (!m_PointerInside) ResetInteractiveLight();
            SetMaterialDirty();
            TranslucentRendererFeature.RequestUpdate();
        }
#endif

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();
            SetMaterialDirty();
        }

        protected override void OnTransformParentChanged()
        {
            m_CaptureSourceKnown = false;
            base.OnTransformParentChanged();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            m_CaptureSourceKnown = false;
            EnsureCanvasChannels();
            SetMaterialDirty();
            TranslucentRendererFeature.RequestUpdate();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            base.OnDidApplyAnimationProperties();
            SetMaterialDirty();
            TranslucentRendererFeature.RequestUpdate();
        }

        private void Update()
        {
            RefreshSpriteField(true);
            RefreshCaptureBinding();
            if (!EnableSpecularGlare || !InteractiveGlare) return;

            float blend = 1f - Mathf.Exp(-LightFollowSpeed * Time.unscaledDeltaTime);
            Vector2 next = Vector2.Lerp(m_CurrentLightDirection, m_TargetLightDirection, blend);
            if ((next - m_CurrentLightDirection).sqrMagnitude < 0.0000001f) return;

            m_CurrentLightDirection = next;
            SetMaterialDirty();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_PointerInside = true;
            UpdatePointerLight(eventData);
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            if (m_PointerInside) UpdatePointerLight(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_PointerInside = false;
            m_TargetLightDirection = ClampLightDirection(LightDirection);
        }

        private void UpdatePointerLight(PointerEventData eventData)
        {
            RectTransform targetRect = rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, eventData.position, eventData.enterEventCamera, out Vector2 localPoint))
                return;

            Rect rect = targetRect.rect;
            float x = Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x) * 2f - 1f;
            float y = Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y) * 2f - 1f;
            m_TargetLightDirection = ClampLightDirection(new Vector2(x, y));
        }

        private void ResetInteractiveLight()
        {
            m_CurrentLightDirection = ClampLightDirection(LightDirection);
            m_TargetLightDirection = m_CurrentLightDirection;
        }

        private static Vector2 ClampLightDirection(Vector2 direction)
        {
            return direction.sqrMagnitude > 1f ? direction.normalized : direction;
        }

        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (UsesSpriteSilhouette)
            {
                RefreshSpriteField();
                if (m_SpriteField == null) return false;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 spriteLocal)) return false;
                if (!ContainsImageGeometry(spriteLocal)) return false;
                Rect drawing = rectTransform.rect;
                Vector2 spriteSize = SilhouetteSprite.rect.size;
                if (preserveAspect && type == Type.Simple)
                {
                    float scale = Mathf.Min(drawing.width / spriteSize.x, drawing.height / spriteSize.y);
                    Vector2 size = spriteSize * scale;
                    drawing = new Rect(drawing.position + (drawing.size - size) * rectTransform.pivot, size);
                }
                Vector2 p = spriteLocal - drawing.position;
                Vector2 canonical = p / drawing.size;
                if (type == Type.Sliced || type == Type.Tiled)
                {
                    Vector4 border = SilhouetteSprite.border;
                    float ppu = pixelsPerUnit * pixelsPerUnitMultiplier;
                    canonical.x = MapSpriteAxis(p.x, drawing.width, spriteSize.x, border.x, border.z, ppu, type == Type.Tiled);
                    canonical.y = MapSpriteAxis(p.y, drawing.height, spriteSize.y, border.y, border.w, ppu, type == Type.Tiled);
                }
                return canonical.x >= 0f && canonical.x <= 1f && canonical.y >= 0f && canonical.y <= 1f && m_SpriteField.Distance((canonical - Vector2.one * 0.5f) * drawing.size, drawing.size) <= SilhouetteRaycastPadding;
            }
            if (!base.IsRaycastLocationValid(screenPoint, eventCamera)) return false;
            if (!ProceduralShape) return true;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 local)) return false;
            if (CornerSettings != null && CornerSettings.Enabled && (EdgeShape == EdgeShape.RoundedRect || EdgeShape == EdgeShape.ContinuousRoundedRect))
                return GlassShapeUtility.CustomCornersDistance(local - rectTransform.rect.center, rectTransform.rect.size, CornerSettings.ResolveRadii(rectTransform.rect.size), CornerSettings.Exponents) <= SilhouetteRaycastPadding;
            return GlassShapeUtility.Distance(local - rectTransform.rect.center, rectTransform.rect.size,
                EdgeShape, EdgeRounding, CornerContinuity, IndividualCorners, CornerRadii) <= SilhouetteRaycastPadding;
        }

        private bool ContainsImageGeometry(Vector2 point)
        {
            if (type != Type.Filled && (fillCenter || (type != Type.Sliced && type != Type.Tiled))) return true;
            using (var helper = new VertexHelper())
            {
                base.OnPopulateMesh(helper);
                m_HitVertices.Clear(); helper.GetUIVertexStream(m_HitVertices);
                for (int i = 0; i + 2 < m_HitVertices.Count; i += 3)
                {
                    Vector2 a = m_HitVertices[i].position, b = m_HitVertices[i + 1].position, c = m_HitVertices[i + 2].position;
                    if (Mathf.Abs(Cross(b - a, c - a)) < 0.000001f) continue;
                    float first = Cross(b - a, point - a), second = Cross(c - b, point - b), third = Cross(a - c, point - c);
                    if ((first >= 0f && second >= 0f && third >= 0f) || (first <= 0f && second <= 0f && third <= 0f)) return true;
                }
            }
            return false;
        }
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static float MapSpriteAxis(float coordinate, float size, float sourceSize, float first, float last, float ppu, bool tile)
        {
            float a = first / ppu, b = last / ppu;
            float fit = Mathf.Min(1f, size / Mathf.Max(a + b, 0.0001f));
            a *= fit; b *= fit;
            if (coordinate <= a && a > 0f) return coordinate / a * first / sourceSize;
            if (coordinate >= size - b && b > 0f) return 1f - (size - coordinate) / b * last / sourceSize;
            float middle = Mathf.Max(0.0001f, sourceSize - first - last);
            float unit = tile ? Mathf.Repeat((coordinate - a) * ppu, middle) / middle : (coordinate - a) / Mathf.Max(0.0001f, size - a - b);
            return (first + unit * middle) / sourceSize;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            base.OnPopulateMesh(vertexHelper);

            Rect rect = rectTransform.rect;
            float width = Mathf.Max(0.0001f, rect.width);
            float height = Mathf.Max(0.0001f, rect.height);
            UIVertex vertex = default;

            for (int i = 0; i < vertexHelper.currentVertCount; i++)
            {
                vertexHelper.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = new Vector4(
                    (vertex.position.x - rect.xMin) / width,
                    (vertex.position.y - rect.yMin) / height,
                    0f,
                    0f);
                vertexHelper.SetUIVertex(vertex, i);
            }
            AddShadowMesh(vertexHelper, rect);
        }

        protected void ScreenPixelBasis(out Vector2 x, out Vector2 y)
        {
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, rectTransform.TransformPoint(rectTransform.rect.center));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, camera, out Vector2 origin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen + Vector2.right, camera, out Vector2 right);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen + Vector2.up, camera, out Vector2 up);
            x = right - origin; y = up - origin;
        }

        protected void AddShadowMesh(VertexHelper helper, Rect bounds)
        {
            if (Surface == null || Surface.ShadowColor.a <= 0f || (Surface.ShadowSize <= 0f && Surface.ShadowOffset == Vector2.zero)) return;
            ScreenPixelBasis(out Vector2 x, out Vector2 y);
            Vector2 offset = x * Surface.ShadowOffset.x + y * Surface.ShadowOffset.y;
            Vector2 extent = new Vector2(Mathf.Abs(x.x) + Mathf.Abs(y.x), Mathf.Abs(x.y) + Mathf.Abs(y.y)) * (Surface.ShadowSize * 3f + 2f);
            bounds = Rect.MinMaxRect(bounds.xMin + offset.x - extent.x, bounds.yMin + offset.y - extent.y, bounds.xMax + offset.x + extent.x, bounds.yMax + offset.y + extent.y);
            m_ShadowVertices.Clear(); helper.GetUIVertexStream(m_ShadowVertices); helper.Clear();
            Rect rect = rectTransform.rect;
            for (int i = 0; i < 4; i++)
            {
                Vector2 position = new Vector2(i < 2 ? bounds.xMin : bounds.xMax, i == 0 || i == 3 ? bounds.yMin : bounds.yMax);
                UIVertex vertex = UIVertex.simpleVert; vertex.position = position; vertex.color = color;
                vertex.uv1 = new Vector4((position.x - rect.xMin) / Mathf.Max(rect.width, 0.0001f), (position.y - rect.yMin) / Mathf.Max(rect.height, 0.0001f), 1f, 0f);
                helper.AddVert(vertex);
            }
            helper.AddTriangle(0, 1, 2); helper.AddTriangle(2, 3, 0);
            for (int i = 0; i < m_ShadowVertices.Count; i++) helper.AddVert(m_ShadowVertices[i]);
            for (int i = 0; i < m_ShadowVertices.Count; i += 3) helper.AddTriangle(4 + i, 5 + i, 6 + i);
        }

        private void EnsureCanvasChannels()
        {
            Canvas targetCanvas = canvas;
            if (targetCanvas == null) return;

            const AdditionalCanvasShaderChannels requiredChannel = AdditionalCanvasShaderChannels.TexCoord1;
            if ((targetCanvas.additionalShaderChannels & requiredChannel) == requiredChannel) return;

            targetCanvas.additionalShaderChannels |= requiredChannel;
            SetVerticesDirty();
        }

        #region Animation Helpers
        
        private Coroutine m_AnimationCoroutine;

        /// <summary>
        /// Instantly enables the GameObject and smoothly animates the complete glass material into existence.
        /// </summary>
        public void FadeIn(float duration = 0.5f)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            AnimateGlassIntensity(1f, duration);
        }

        /// <summary>
        /// Smoothly fades out the glass material. Optionally disables the GameObject when finished.
        /// </summary>
        public void FadeOut(float duration = 0.5f, bool disableOnComplete = false)
        {
            AnimateGlassIntensity(0f, duration, disableOnComplete);
        }

        /// <summary>
        /// Animates the master Glass Intensity slider, perfectly syncing Blur, Frost, and Edge Lighting over time.
        /// </summary>
        public void AnimateGlassIntensity(float targetIntensity, float duration, bool disableOnComplete = false)
        {
            targetIntensity = Mathf.Clamp01(targetIntensity);
            if (!Application.isPlaying)
            {
                GlassIntensity = targetIntensity;
                SetMaterialDirty();
                if (disableOnComplete && targetIntensity <= 0f) gameObject.SetActive(false);
                return;
            }

            if (m_AnimationCoroutine != null) StopCoroutine(m_AnimationCoroutine);
            if (duration <= 0.0001f)
            {
                GlassIntensity = targetIntensity;
                SetMaterialDirty();
                if (disableOnComplete && targetIntensity <= 0.001f) gameObject.SetActive(false);
                return;
            }

            if (gameObject.activeInHierarchy)
            {
                m_AnimationCoroutine = StartCoroutine(SmoothGlassRoutine(targetIntensity, duration, disableOnComplete));
            }
        }

        private System.Collections.IEnumerator SmoothGlassRoutine(float target, float duration, bool disable)
        {
            float start = GlassIntensity;
            float time = 0f;
            
            while (time < duration)
            {
                time += Time.unscaledDeltaTime;
                float t = time / duration;
                
                // Smoothstep curve for cinematic premium easing
                t = t * t * (3f - 2f * t);
                
                GlassIntensity = Mathf.Lerp(start, target, t);
                SetMaterialDirty();
                yield return null;
            }

            GlassIntensity = target;
            SetMaterialDirty();
            
            if (disable && target <= 0.001f)
            {
                gameObject.SetActive(false);
            }
        }
        
        #endregion

        private static Material s_DefaultTranslucentMaterial;
        private static ComputeBuffer s_EmptyFusionBuffer;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeSharedResources()
        {
            ReleaseSharedResources();
            // Subsystem registration also runs when domain reload is disabled.
            Application.quitting -= ReleaseSharedResources;
            Application.quitting += ReleaseSharedResources;
        }

        private static void ReleaseSharedResources()
        {
            s_EmptyFusionBuffer?.Release(); s_EmptyFusionBuffer = null;
            if (s_DefaultTranslucentMaterial != null)
            {
                if (Application.isPlaying) Destroy(s_DefaultTranslucentMaterial); else DestroyImmediate(s_DefaultTranslucentMaterial);
                s_DefaultTranslucentMaterial = null;
            }
        }
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterSharedCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseSharedResources;
            UnityEditor.EditorApplication.quitting += ReleaseSharedResources;
        }
#endif


        public override Material defaultMaterial
        {
            get
            {
                Material translucentMaterial = GetDefaultMaterial();
                return translucentMaterial != null ? translucentMaterial : base.defaultMaterial;
            }
        }

        public static Material GetDefaultMaterial()
        {
            if (s_DefaultTranslucentMaterial == null)
            {
                Shader shader = Shader.Find("UI/TranslucentUIFX");
                if (shader != null)
                {
                    s_DefaultTranslucentMaterial = new Material(shader);
                    s_DefaultTranslucentMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            return s_DefaultTranslucentMaterial;
        }

        public override Material materialForRendering
        {
            get
            {
                Material baseMat = base.materialForRendering;

                if (!isActiveAndEnabled || baseMat == null)
                    return baseMat;

                if (baseMat.shader == null || baseMat.shader.name != "UI/TranslucentUIFX")
                {
                    baseMat = GetDefaultMaterial();
                    if (baseMat == null) return null;
                }

                if (m_CustomMaterial == null || m_CustomMaterial.shader != baseMat.shader)
                {
                    m_CustomMaterial = new Material(baseMat);
                    m_CustomMaterial.hideFlags = HideFlags.HideAndDontSave;
                }
                
                m_CustomMaterial.CopyPropertiesFromMaterial(baseMat);

                m_CustomMaterial.SetFloat("_Brightness", Brightness);
                m_CustomMaterial.SetFloat("_Saturation", Saturation);
                m_CustomMaterial.SetFloat("_Contrast", Contrast);
                m_CustomMaterial.SetFloat("_GlassIntensity", GlassIntensity);
                
                m_CustomMaterial.SetFloat("_AutoReadability", AutoReadability ? 0.75f : 0.0f);

                m_CustomMaterial.SetFloat("_LuminosityBoost", LuminosityBoost);
                
                m_CustomMaterial.SetFloat("_BlurStrength", BlurStrength * GlassIntensity);
                
                Color finalTint = TintColor;
                finalTint.a *= GlassIntensity;
                m_CustomMaterial.SetColor("_TintColor", finalTint);
                
                m_CustomMaterial.SetFloat("_FrostAmount", FrostAmount * GlassIntensity);
                m_CustomMaterial.SetFloat("_NoiseAmount", NoiseAmount * GlassIntensity);
                m_CustomMaterial.SetFloat("_RefractionAmount", RefractionAmount * GlassIntensity);
                m_CustomMaterial.SetFloat("_RefractiveIndex", RefractiveIndex);
                m_CustomMaterial.SetFloat("_SphericalDistortion", SphericalDistortion ? 1.0f : 0.0f);
                m_CustomMaterial.SetFloat("_ChromaticAberration", ChromaticAberration * GlassIntensity);
                m_CustomMaterial.SetFloat("_AlphaGradientStability", StabilizeAlphaGradients ? 1.0f : 0.0f);
                m_CustomMaterial.SetFloat("_SpecularGlare", EnableSpecularGlare ? SpecularGlare * GlassIntensity : 0f);
                m_CustomMaterial.SetFloat("_SpecularSharpness", SpecularSharpness);
                m_CustomMaterial.SetFloat("_RimDepth", RimDepth * GlassIntensity);
                m_CustomMaterial.SetVector("_LightDirection", new Vector4(m_CurrentLightDirection.x, m_CurrentLightDirection.y, 0f, 0f));

                Rect rect = rectTransform.rect;
                float width = Mathf.Max(0.0001f, rect.width);
                float height = Mathf.Max(0.0001f, rect.height);
                m_CustomMaterial.SetVector("_RectSize", new Vector4(width, height, 0f, 0f));
                Surface ??= new GlassSurfaceSettings();
                m_CustomMaterial.SetVector("_SurfaceOptics", new Vector4(Surface.Magnification, Surface.Transmission, Surface.ResolveLip(rect.size) / Mathf.Min(width, height), Surface.Smoothness));
                m_CustomMaterial.SetVector("_SurfaceLighting", new Vector4((float)Surface.LightMode, Surface.OpposingStrength, Surface.LightSpread, Surface.PointRadius));
                m_CustomMaterial.SetVector("_PointLightPosition", Surface.PointPosition);
                Vector2 bands = Surface.ResolveBands(rect.size);
                m_CustomMaterial.SetVector("_LipBands", new Vector4(bands.x, bands.y, Surface.StabilizeInterior ? 1f : 0f, 0f));
                m_CustomMaterial.SetFloat("_LensDepth", Surface.LensDepth);
                m_CustomMaterial.SetColor("_LipLightColor", EnableEdgeLighting ? Surface.LightColor : Color.clear);
                m_CustomMaterial.SetColor("_LipShadowColor", Surface.LipShadow);
                ScreenPixelBasis(out Vector2 pixelX, out Vector2 pixelY);
                Vector2 shadowOffset = pixelX * Surface.ShadowOffset.x + pixelY * Surface.ShadowOffset.y;
                m_CustomMaterial.SetVector("_ShadowSettings", new Vector4(Surface.ShadowSize, shadowOffset.x / width, shadowOffset.y / height, 0));
                m_CustomMaterial.SetColor("_ShadowColor", Surface.ShadowColor);
                Rect drawing = rect;
                if (UsesSpriteSilhouette && preserveAspect && type == Type.Simple)
                {
                    Vector2 source = SilhouetteSprite.rect.size;
                    Vector2 size = source * Mathf.Min(width / source.x, height / source.y);
                    drawing = new Rect(rect.position + (rect.size - size) * rectTransform.pivot, size);
                }
                m_CustomMaterial.SetVector("_SpriteDrawingRect", new Vector4((drawing.x - rect.x) / width, (drawing.y - rect.y) / height, drawing.width / width, drawing.height / height));
                RefreshSpriteField();
                bool spriteFieldReady = m_SpriteField != null && m_SpriteField.Texture != null;
                m_CustomMaterial.SetFloat("_SpriteShape", spriteFieldReady ? 1f : 0f);
                m_CustomMaterial.SetFloat("_SpriteAlphaThreshold", SpriteAlphaThreshold);
                if (spriteFieldReady)
                {
                    m_CustomMaterial.SetTexture("_SpriteDistanceTex", m_SpriteField.Texture);
                    m_CustomMaterial.SetVector("_SpriteUvRow0", m_SpriteField.UvRow0);
                    m_CustomMaterial.SetVector("_SpriteUvRow1", m_SpriteField.UvRow1);
                }
                m_CustomMaterial.SetFloat("_ProceduralShape", UsesSpriteSilhouette ? 0f : ProceduralShape ? 1f : 0f);
                m_CustomMaterial.SetFloat("_CornerContinuity", CornerContinuity);

                float shapeIndex = (float)EdgeShape;
                m_CustomMaterial.SetFloat("_EdgeShape", shapeIndex);
                bool customCorners = CornerSettings != null && CornerSettings.Enabled && (EdgeShape == EdgeShape.RoundedRect || EdgeShape == EdgeShape.ContinuousRoundedRect);
                m_CustomMaterial.SetFloat("_CustomCorners", customCorners ? 1f : 0f);
                m_CustomMaterial.SetVector("_CornerProfiles", customCorners ? CornerSettings.Exponents : Vector4.one * 2f);
                m_CustomMaterial.SetVector("_CornerRadii", customCorners ? CornerSettings.ResolveRadii(rect.size) : IndividualCorners ? GlassShapeUtility.ClampCorners(CornerRadii) : Vector4.one * EdgeRounding);
                m_CustomMaterial.SetFloat("_EdgeRounding", EdgeRounding);
                
                if (EnableEdgeLighting)
                {
                    Color finalEdge = EdgeLightColor;
                    finalEdge.a *= GlassIntensity;
                    m_CustomMaterial.SetColor("_EdgeColor", finalEdge);
                    m_CustomMaterial.SetFloat("_EdgeWidth", EdgeLightWidth);
                    m_CustomMaterial.SetFloat("_EdgePower", EdgeLightPower);
                }
                else
                {
                    m_CustomMaterial.SetColor("_EdgeColor", Color.clear);
                    m_CustomMaterial.SetFloat("_EdgeWidth", 0f);
                    m_CustomMaterial.SetFloat("_EdgePower", 1f);
                }

                if (s_EmptyFusionBuffer == null)
                {
                    s_EmptyFusionBuffer = new ComputeBuffer(1, TranslucentGlassGroup.ElementStride, ComputeBufferType.Structured);
                    s_EmptyFusionBuffer.SetData(new[] { new TranslucentGlassGroup.Element() });
                }
                m_CustomMaterial.SetBuffer("_FusionElements", s_EmptyFusionBuffer);
                ConfigureMaterial(m_CustomMaterial);
                RefreshCaptureBinding(true);
                
                return m_CustomMaterial;
            }
        }

        /// <summary>
        /// Extension point for specialized glass surfaces that share the same capture and
        /// optical stack. Called after the standard material properties are configured.
        /// </summary>
        protected virtual void ConfigureMaterial(Material targetMaterial)
        {
            targetMaterial.SetFloat("_FusionEnabled", 0f);
            targetMaterial.SetFloat("_FusionCount", 0f);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ActiveInstances.Remove(this);
            GlassSpriteField.Release(m_SpriteField); m_SpriteField = null;
            if (m_CustomMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(m_CustomMaterial);
                else
                    DestroyImmediate(m_CustomMaterial);
            }
        }
    }
}
