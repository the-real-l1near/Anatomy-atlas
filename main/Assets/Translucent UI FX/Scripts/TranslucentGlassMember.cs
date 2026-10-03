using UnityEngine;

namespace TranslucentUIFX
{
    public enum GlassOperation { Add, Cutout }

    /// <summary>
    /// Defines one analytic shape inside a <see cref="TranslucentGlassGroup"/>.
    /// The member keeps its normal RectTransform and interaction components; the parent
    /// group owns the single glass surface that visually fuses all members together.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    [AddComponentMenu("UI/Translucent Glass Member")]
    public sealed class TranslucentGlassMember : MonoBehaviour, ICanvasRaycastFilter
    {
        public GlassCornerSettings CornerSettings = new GlassCornerSettings();
        public GlassMemberAppearance Appearance = new GlassMemberAppearance();
        [SerializeField] private Sprite m_ShapeSprite;
        [SerializeField] private bool m_PreserveAspect;
        [SerializeField, Range(0.01f, 0.99f)] private float m_AlphaThreshold = 0.5f;
        public float AlphaThreshold { get => m_AlphaThreshold; set { m_AlphaThreshold = Mathf.Clamp(value, 0.01f, 0.99f); NotifyOwner(); } }
        private GlassSpriteField.Entry m_Field;
        public Sprite ShapeSprite { get => m_ShapeSprite; set { if (m_ShapeSprite == value) return; m_ShapeSprite = value; NotifyOwner(); } }
        public bool PreserveAspect { get => m_PreserveAspect; set { m_PreserveAspect = value; NotifyOwner(); } }
        internal GlassSpriteField.Entry SpriteField
        {
            get
            {
                int size = (int)(Owner != null ? Owner.SpriteFieldResolution : GlassFieldResolution.Standard);
                if (m_Field != null && m_Field.Matches(m_ShapeSprite, m_AlphaThreshold, size)) return m_Field;
                GlassSpriteField.Release(m_Field);
                m_Field = GlassSpriteField.Acquire(m_ShapeSprite, m_AlphaThreshold, size);
                return m_Field;
            }
        }
        public Rect ShapeRect
        {
            get
            {
                Rect rect = RectTransform.rect;
                if (m_ShapeSprite == null || !m_PreserveAspect) return rect;
                Vector2 source = m_ShapeSprite.rect.size;
                Vector2 size = source * Mathf.Min(rect.width / source.x, rect.height / source.y);
                return new Rect(rect.position + (rect.size - size) * RectTransform.pivot, size);
            }
        }
        public float Distance(Vector2 point)
        {
            Rect rect = ShapeRect;
            if (m_ShapeSprite != null) return SpriteField?.Distance(point - rect.center, rect.size) ?? float.PositiveInfinity;
            if (CornerSettings != null && CornerSettings.Enabled && (Shape == EdgeShape.RoundedRect || Shape == EdgeShape.ContinuousRoundedRect))
                return GlassShapeUtility.CustomCornersDistance(point - rect.center, rect.size, CornerSettings.ResolveRadii(rect.size), CornerSettings.Exponents);
            return GlassShapeUtility.Distance(point - rect.center, rect.size, Shape, CornerRadius, CornerContinuity, IndividualCorners, CornerRadii);
        }

        [SerializeField] private bool m_Include = true;
        [SerializeField] private GlassOperation m_Operation;
        [SerializeField] private bool m_IndividualCorners;
        [SerializeField] private Vector4 m_CornerRadii = Vector4.one * 0.22f;
        [SerializeField] private EdgeShape m_Shape = EdgeShape.ContinuousRoundedRect;
        [SerializeField, Range(0f, 0.5f)] private float m_CornerRadius = 0.22f;
        [SerializeField, Range(0f, 1f)] private float m_CornerContinuity = 0.82f;
        [SerializeField, Range(-32f, 64f)] private float m_SurfaceExpansion;

#if UNITY_EDITOR
        // One-click fusion replaces the standalone glass Graphic with a transparent
        // interaction Image. Keeping its serialized state here makes Detach Member
        // lossless while adding no data to player builds.
        [SerializeField, HideInInspector] private string m_EditorStandaloneJson;
#endif

        private RectTransform m_RectTransform;
        private TranslucentGlassGroup m_Owner;

        public GlassOperation Operation { get => m_Operation; set { m_Operation = value; NotifyOwner(); } }
        public bool IndividualCorners { get => m_IndividualCorners; set { m_IndividualCorners = value; NotifyOwner(); } }
        public Vector4 CornerRadii { get => m_CornerRadii; set { m_CornerRadii = GlassShapeUtility.ClampCorners(value); NotifyOwner(); } }
        public TranslucentGlassGroup Owner => GetComponentInParent<TranslucentGlassGroup>();

        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
        {
            if (!isActiveAndEnabled) return true;
            if (!Include || Operation == GlassOperation.Cutout) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(RectTransform, screenPoint, eventCamera, out Vector2 point)) return false;
            // Preserve each control's own silhouette, then remove holes cut into the group.
            bool inside = Distance(point) <= SurfaceExpansion;
            return inside && (Owner == null || Owner.ContainsScreenPoint(screenPoint, eventCamera));
        }

        public bool Include
        {
            get => m_Include;
            set
            {
                if (m_Include == value) return;
                m_Include = value;
                NotifyOwner();
            }
        }

        public EdgeShape Shape
        {
            get => m_Shape;
            set
            {
                if (m_Shape == value) return;
                m_Shape = value;
                NotifyOwner();
            }
        }

        public float CornerRadius
        {
            get => m_CornerRadius;
            set
            {
                value = Mathf.Clamp(value, 0f, 0.5f);
                if (Mathf.Approximately(m_CornerRadius, value)) return;
                m_CornerRadius = value;
                NotifyOwner();
            }
        }

        public float CornerContinuity
        {
            get => m_CornerContinuity;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(m_CornerContinuity, value)) return;
                m_CornerContinuity = value;
                NotifyOwner();
            }
        }

        public float SurfaceExpansion
        {
            get => m_SurfaceExpansion;
            set
            {
                value = Mathf.Clamp(value, -32f, 64f);
                if (Mathf.Approximately(m_SurfaceExpansion, value)) return;
                m_SurfaceExpansion = value;
                NotifyOwner();
            }
        }

        public RectTransform RectTransform
        {
            get
            {
                if (m_RectTransform == null)
                    m_RectTransform = (RectTransform)transform;
                return m_RectTransform;
            }
        }

#if UNITY_EDITOR
        /// <summary>Editor-only source state used by the reversible fusion workflow.</summary>
        public string EditorStandaloneJson
        {
            get => m_EditorStandaloneJson;
            set => m_EditorStandaloneJson = value;
        }
#endif

        internal void AssignOwner(TranslucentGlassGroup owner)
        {
            m_Owner = owner;
        }

        private void OnEnable()
        {
            NotifyOwner();
        }

        private void OnDisable()
        {
            GlassSpriteField.Release(m_Field); m_Field = null;
            NotifyOwner();
        }

        private void OnTransformParentChanged()
        {
            m_Owner = null;
            NotifyOwner();
        }

        private void OnRectTransformDimensionsChange()
        {
            NotifyOwner();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            m_CornerRadii = GlassShapeUtility.ClampCorners(m_CornerRadii);
            m_CornerRadius = Mathf.Clamp(m_CornerRadius, 0f, 0.5f);
            m_CornerContinuity = Mathf.Clamp01(m_CornerContinuity);
            m_SurfaceExpansion = Mathf.Clamp(m_SurfaceExpansion, -32f, 64f);
            NotifyOwner();
        }
#endif

        private void NotifyOwner()
        {
            if (m_Owner == null)
                m_Owner = GetComponentInParent<TranslucentGlassGroup>();

            m_Owner?.NotifyMemberChanged();
        }
    }
}
