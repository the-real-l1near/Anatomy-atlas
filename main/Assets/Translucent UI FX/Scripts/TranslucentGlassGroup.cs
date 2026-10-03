using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TranslucentUIFX
{
    /// <summary>A single glass surface composed from independently movable child shapes.</summary>
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("UI/Liquid Glass Fusion")]
    public sealed class TranslucentGlassGroup : TranslucentImageFX
    {
        [System.Obsolete("Fusion capacity grows with the active collection.")] public const int MaxMembers = int.MaxValue;
        [SerializeField, Range(0f, 96f)] private float m_FusionSoftness = 28f;
        [SerializeField, Range(-32f, 64f)] private float m_SurfacePadding = 2f;
        [SerializeField, HideInInspector] private List<TranslucentGlassMember> m_Members = new List<TranslucentGlassMember>(8);
        private Vector4[] m_Rects = new Vector4[8];
        private Vector4[] m_Data = new Vector4[8];
        private Vector4[] m_Transforms = new Vector4[8];
        private Vector4[] m_Corners = new Vector4[8];
        private Vector4[] m_Metrics = new Vector4[8];
        private Texture2DArray m_SpriteFields;
        private Texture2D[] m_FieldTextures = new Texture2D[1];
        private readonly Dictionary<Texture2D, int> m_SpriteSlots = new Dictionary<Texture2D, int>();
        private readonly List<GlassSpriteField.Entry> m_PendingFields = new List<GlassSpriteField.Entry>();
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        internal struct Element { public Vector4 Rect, Data, Transform, Corners, Metrics, Tint, Optics, Surface, Style, Profiles, Graphic, Bands; }
        internal const int ElementStride = 192;
        private Element[] m_Elements = new Element[8];
        private ComputeBuffer m_ElementBuffer;
        private bool m_BufferDirty = true;
        private readonly Vector3[] m_WorldCorners = new Vector3[4];
        private readonly List<TranslucentGlassMember> m_Scan = new List<TranslucentGlassMember>();
        private bool m_RefreshQueued;
        private int m_ActiveMemberCount;
        private Rect m_RenderBounds;
        private static readonly int RectsId = Shader.PropertyToID("_FusionRects");
        private static readonly int DataId = Shader.PropertyToID("_FusionData");
        private static readonly int TransformsId = Shader.PropertyToID("_FusionTransforms");
        private static readonly int CornersId = Shader.PropertyToID("_FusionCorners");
        private static readonly int MetricsId = Shader.PropertyToID("_FusionMetrics");

        public float FusionSoftness { get => m_FusionSoftness; set { m_FusionSoftness = Mathf.Clamp(value, 0f, 96f); SetAllDirty(); } }
        public float SurfacePadding { get => m_SurfacePadding; set { m_SurfacePadding = Mathf.Clamp(value, -32f, 64f); SetAllDirty(); } }
        public int MemberCount => m_Members.Count;
        public int ActiveMemberCount => m_ActiveMemberCount;
        [System.Obsolete("Fusion capacity is dynamic.")] public bool IsAtMemberLimit => false;
        public IReadOnlyList<TranslucentGlassMember> Members => m_Members;

        public override float RequiredBlurStrength
        {
            get
            {
                float strength = BlurStrength;
                foreach (var member in m_Members)
                    if (member != null && member.isActiveAndEnabled && member.Include && member.Appearance != null && member.Appearance.Override)
                        strength = Mathf.Max(strength, member.Appearance.Blur);
                return strength;
            }
        }

        protected override void OnEnable() { RefreshMembers(); base.OnEnable(); }
        protected override void OnDisable()
        {
            if (m_SpriteFields != null) { if (Application.isPlaying) Destroy(m_SpriteFields); else DestroyImmediate(m_SpriteFields); }
            m_SpriteFields = null;
            System.Array.Clear(m_FieldTextures, 0, m_FieldTextures.Length);
            m_ElementBuffer?.Release(); m_ElementBuffer = null; m_BufferDirty = true;
            base.OnDisable();
        }
        protected override void OnRectTransformDimensionsChange() { base.OnRectTransformDimensionsChange(); SetAllDirty(); }
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            m_FusionSoftness = Mathf.Clamp(m_FusionSoftness, 0f, 96f);
            m_SurfacePadding = Mathf.Clamp(m_SurfacePadding, -32f, 64f);
            m_RefreshQueued = true;
            base.OnValidate();
        }
        // UIBehaviour.Reset exists only in Editor builds. Keep authoring defaults out of players.
        protected override void Reset()
        {
            base.Reset();
            ApplyLiquidGlass();
            raycastTarget = false;
            RefreshMembers();
        }
#endif
        private void LateUpdate()
        {
            if (m_RefreshQueued) RefreshMembers();
            if (UpdateFusionData()) SetAllDirty();
        }
        private void OnTransformChildrenChanged() { m_RefreshQueued = true; }
        internal void NotifyMemberChanged() { m_RefreshQueued = true; SetAllDirty(); }

        public void RefreshMembers()
        {
            m_RefreshQueued = false;
            m_Members.Clear();
            GetComponentsInChildren(true, m_Scan);
            foreach (TranslucentGlassMember member in m_Scan)
            {
                // Nested groups own their own members and never contribute twice.
                if (member.Owner != this) continue;
                m_Members.Add(member);
                member.AssignOwner(this);
            }
            UpdateFusionData();
            SetAllDirty();
        }

        protected override void ConfigureMaterial(Material material)
        {
            UpdateFusionData();
            material.SetTexture("_FusionSpriteFields", m_SpriteFields);
            material.SetVector("_FusionFieldSize", new Vector4((int)SpriteFieldResolution, (int)SpriteFieldResolution + GlassSpriteField.Padding * 2, 0, 0));
            material.SetFloat("_FusionEnabled", 1f);
            if (m_ElementBuffer == null || m_ElementBuffer.count < m_Elements.Length)
            {
                m_ElementBuffer?.Release();
                m_ElementBuffer = new ComputeBuffer(m_Elements.Length, ElementStride, ComputeBufferType.Structured);
                m_BufferDirty = true;
            }
            if (m_BufferDirty) { m_ElementBuffer.SetData(m_Elements); m_BufferDirty = false; }
            material.SetBuffer("_FusionElements", m_ElementBuffer);
            material.SetFloat("_FusionCount", m_ActiveMemberCount);
            material.SetFloat("_FusionSoftness", m_FusionSoftness);
            ScreenPixelBasis(out Vector2 pixelX, out Vector2 pixelY);
            float shadowSupport = Surface != null && Surface.ShadowColor.a > 0f ? Surface.ShadowSize * 3f * Mathf.Max(pixelX.magnitude, pixelY.magnitude) : 0f;
            material.SetFloat("_FusionSupport", m_FusionSoftness + shadowSupport + 4f);





        }

        private bool UpdateFusionData()
        {
            bool changed = false;
            if (m_Members.Count > m_Elements.Length)
            {
                int capacity = Mathf.NextPowerOfTwo(m_Members.Count);
                System.Array.Resize(ref m_Elements, capacity);
                System.Array.Resize(ref m_Rects, capacity); System.Array.Resize(ref m_Data, capacity);
                System.Array.Resize(ref m_Transforms, capacity); System.Array.Resize(ref m_Corners, capacity); System.Array.Resize(ref m_Metrics, capacity);
                changed = true;
            }
            m_SpriteSlots.Clear(); m_PendingFields.Clear();
            int active = 0;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (TranslucentGlassMember member in m_Members)
            {
                if (member == null || !member.isActiveAndEnabled || !member.Include) continue;
                int index = active++;
                RectTransform rect = member.RectTransform;
                Rect shapeRect = member.ShapeRect;
                Vector2 center = rectTransform.InverseTransformPoint(rect.TransformPoint(shapeRect.center));
                center -= rectTransform.rect.center;
                Vector2 x = rectTransform.InverseTransformVector(rect.TransformVector(Vector3.right));
                Vector2 y = rectTransform.InverseTransformVector(rect.TransformVector(Vector3.up));
                float determinant = x.x * y.y - y.x * x.y;
                float scale = Mathf.Max(0.0001f, Mathf.Min(x.magnitude, y.magnitude));
                Vector4 inverse = Mathf.Abs(determinant) < 0.000001f ? Vector4.zero : new Vector4(y.y, -y.x, -x.y, x.x) / determinant;
                Vector4 bounds = new Vector4(center.x, center.y, shapeRect.width, shapeRect.height);
                Vector4 data = new Vector4((float)member.Shape, member.CornerRadius, member.CornerContinuity, member.SurfaceExpansion + m_SurfacePadding);
                bool customCorners = member.CornerSettings != null && member.CornerSettings.Enabled && (member.Shape == EdgeShape.RoundedRect || member.Shape == EdgeShape.ContinuousRoundedRect);
                Vector4 corners = customCorners ? member.CornerSettings.ResolveRadii(shapeRect.size) : member.IndividualCorners ? member.CornerRadii : Vector4.one * member.CornerRadius;
                GlassSpriteField.Entry field = member.SpriteField;
                int slice = -1;
                if (field != null && field.Texture != null)
                {
                    if (!m_SpriteSlots.TryGetValue(field.Texture, out slice))
                    {
                        slice = m_PendingFields.Count;
                        m_SpriteSlots.Add(field.Texture, slice);
                        m_PendingFields.Add(field);
                    }
                }
                Vector4 metrics = new Vector4((float)member.Operation, scale, slice + 1f, customCorners ? 1f : 0f);
                GlassMemberAppearance appearance = member.Appearance;
                bool own = appearance != null && appearance.Override;
                GlassSurfaceSettings optics = own ? appearance.Surface : Surface;
                optics ??= new GlassSurfaceSettings();
                Vector2 bands = optics.ResolveBands(shapeRect.size);
                float shortSide = Mathf.Max(0.0001f, Mathf.Min(shapeRect.width, shapeRect.height));
                var element = new Element { Profiles = customCorners ? member.CornerSettings.Exponents : Vector4.one * 2f, Rect = bounds, Data = data, Transform = inverse, Corners = corners, Metrics = metrics,
                    Graphic = own ? appearance.GraphicColor : Color.white,
                    Bands = new Vector4(bands.x, bands.y, optics.StabilizeInterior ? 1f : 0f, 0f),
                    Tint = own ? appearance.Tint : TintColor,
                    Optics = new Vector4(own ? appearance.Blur : BlurStrength, own ? appearance.Refraction : RefractionAmount, own ? appearance.Dispersion : ChromaticAberration, optics.LensDepth),
                    Surface = new Vector4(optics.Magnification, optics.Transmission, optics.ResolveLip(shapeRect.size) / shortSide, optics.Smoothness),
                    Style = new Vector4(own ? appearance.Opacity / Mathf.Max(0.0001f, appearance.OpacityReference) : 1f, own ? appearance.RefractiveIndex : RefractiveIndex, own ? appearance.Highlights : EnableSpecularGlare ? SpecularGlare : 0f, own ? appearance.EdgeDepth : RimDepth) };
                Element previous = m_Elements[index];
                changed |= previous.Graphic != element.Graphic || previous.Bands != element.Bands || previous.Profiles != element.Profiles || previous.Tint != element.Tint || previous.Optics != element.Optics || previous.Surface != element.Surface || previous.Style != element.Style;
                m_Elements[index] = element;
                changed |= UpdateVector(m_Rects, index, bounds) | UpdateVector(m_Data, index, data) |
                    UpdateVector(m_Transforms, index, inverse) | UpdateVector(m_Corners, index, corners) | UpdateVector(m_Metrics, index, metrics);
                if (member.Operation == GlassOperation.Cutout) continue;
                rect.GetWorldCorners(m_WorldCorners);
                float padding = Mathf.Max(0f, data.w) + m_FusionSoftness + 2f;
                foreach (Vector3 corner in m_WorldCorners)
                {
                    Vector2 local = rectTransform.InverseTransformPoint(corner);
                    minimum = Vector2.Min(minimum, local - Vector2.one * padding);
                    maximum = Vector2.Max(maximum, local + Vector2.one * padding);
                }
            }
            Rect nextBounds = float.IsInfinity(minimum.x) ? new Rect() : Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
            changed |= nextBounds != m_RenderBounds || active != m_ActiveMemberCount;
            changed |= UpdateSpriteArray();
            m_BufferDirty |= changed;
            m_RenderBounds = nextBounds;
            m_ActiveMemberCount = active;
            return changed;
        }

        private bool UpdateSpriteArray()
        {
            if (m_PendingFields.Count == 0) return false;
            int extent = m_PendingFields[0].Extent;
            bool changed = false;
            if (m_SpriteFields == null || m_SpriteFields.width != extent || m_SpriteFields.depth < m_PendingFields.Count)
            {
                if (m_SpriteFields != null) { if (Application.isPlaying) Destroy(m_SpriteFields); else DestroyImmediate(m_SpriteFields); }
                int capacity = Mathf.NextPowerOfTwo(m_PendingFields.Count);
                m_SpriteFields = new Texture2DArray(extent, extent, capacity, TextureFormat.RFloat, true, true)
                { name = "Fusion sprite fields", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                m_SpriteFields.Apply(false, true);
                m_FieldTextures = new Texture2D[capacity];
                changed = true;
            }
            for (int i = 0; i < m_PendingFields.Count; i++)
            {
                Texture2D texture = m_PendingFields[i].Texture;
                if (m_FieldTextures[i] == texture) continue;
                for (int mip = 0; mip < texture.mipmapCount; mip++) Graphics.CopyTexture(texture, 0, mip, m_SpriteFields, i, mip);
                m_FieldTextures[i] = texture; changed = true;
            }
            return changed;
        }

        private static bool UpdateVector(Vector4[] data, int index, Vector4 value)
        {
            if ((data[index] - value).sqrMagnitude < 0.0000001f) return false;
            data[index] = value;
            return true;
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            UpdateFusionData();
            helper.Clear();
            if (m_RenderBounds.width <= 0f || m_RenderBounds.height <= 0f) return;
            Rect rect = rectTransform.rect;
            for (int i = 0; i < 4; i++)
            {
                Vector2 p = new Vector2(i < 2 ? m_RenderBounds.xMin : m_RenderBounds.xMax, i == 0 || i == 3 ? m_RenderBounds.yMin : m_RenderBounds.yMax);
                UIVertex vertex = UIVertex.simpleVert;
                vertex.position = p;
                vertex.color = color;
                vertex.uv0 = new Vector2(i < 2 ? 0f : 1f, i == 0 || i == 3 ? 0f : 1f);
                vertex.uv1 = new Vector2((p.x - rect.xMin) / Mathf.Max(rect.width, 0.0001f), (p.y - rect.yMin) / Mathf.Max(rect.height, 0.0001f));
                helper.AddVert(vertex);
            }
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);
            AddShadowMesh(helper, m_RenderBounds);
        }

        public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) => ContainsScreenPoint(screenPoint, eventCamera);

        public bool ContainsScreenPoint(Vector2 screenPoint, Camera eventCamera)
        {
            if (m_RefreshQueued) RefreshMembers();
            float added = float.PositiveInfinity, removed = float.PositiveInfinity;
            foreach (TranslucentGlassMember member in m_Members)
            {
                if (member == null || !member.isActiveAndEnabled || !member.Include) continue;
                RectTransform rect = member.RectTransform;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out Vector2 point)) continue;
                float scale = Mathf.Max(0.0001f, Mathf.Min(rectTransform.InverseTransformVector(rect.TransformVector(Vector3.right)).magnitude,
                    rectTransform.InverseTransformVector(rect.TransformVector(Vector3.up)).magnitude));
                float distance = member.Distance(point) * scale - member.SurfaceExpansion - m_SurfacePadding;
                if (member.Operation == GlassOperation.Cutout) removed = GlassShapeUtility.SmoothUnion(removed, distance, m_FusionSoftness);
                else added = GlassShapeUtility.SmoothUnion(added, distance, m_FusionSoftness);
            }
            return -GlassShapeUtility.SmoothUnion(-added, removed, m_FusionSoftness) <= 0f;
        }
    }
}
