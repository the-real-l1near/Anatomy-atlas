using System;
using UnityEngine;

namespace TranslucentUIFX
{
    [Serializable]
    public sealed class GlassMemberAppearance
    {
        public bool Override;
        [HideInInspector] public float OpacityReference = 1f;
        public Color Tint = new Color(1f, 1f, 1f, 0.035f);
        public Color GraphicColor = Color.white;
        [Range(0f, 1f)] public float Blur = 0.08f;
        [Range(0f, 0.2f)] public float Refraction = 0.035f;
        [Range(0f, 0.02f)] public float Dispersion = 0.0015f;
        [Range(0f, 1f)] public float Opacity = 1f;
        [Range(1f, 2.5f)] public float RefractiveIndex = 1.46f;
        [Range(0f, 1f)] public float Highlights = 0.18f;
        [Range(0f, 1f)] public float EdgeDepth = 0.18f;
        public GlassSurfaceSettings Surface = new GlassSurfaceSettings();
        public static GlassMemberAppearance From(TranslucentImageFX source) => new GlassMemberAppearance
        {
            Tint = source.TintColor, GraphicColor = source.color, Blur = source.BlurStrength, Refraction = source.RefractionAmount,
            Dispersion = source.ChromaticAberration, Opacity = source.GlassIntensity, RefractiveIndex = source.RefractiveIndex,
            Highlights = source.EnableSpecularGlare ? source.SpecularGlare : 0f, EdgeDepth = source.RimDepth,
            Surface = JsonUtility.FromJson<GlassSurfaceSettings>(JsonUtility.ToJson(source.Surface))
        };
        public void ApplyTo(TranslucentImageFX target)
        {
            target.TintColor = Tint; target.color = GraphicColor; target.BlurStrength = Blur; target.RefractionAmount = Refraction;
            target.ChromaticAberration = Dispersion; target.GlassIntensity = Opacity; target.RefractiveIndex = RefractiveIndex;
            target.SpecularGlare = Highlights; target.EnableSpecularGlare = Highlights > 0f; target.RimDepth = EdgeDepth;
            target.Surface = JsonUtility.FromJson<GlassSurfaceSettings>(JsonUtility.ToJson(Surface));
        }
    }
}
