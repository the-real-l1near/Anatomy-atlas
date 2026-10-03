using System;
using UnityEngine;

namespace TranslucentUIFX
{
    public enum GlassLengthUnits { CanvasUnits, PercentOfShortSide, PercentOfLongSide }
    public enum GlassBandUnits { FractionOfLip, CanvasUnits }
    public enum GlassLightMode { Directional, Opposing, Point }

    /// <summary>Optional art direction. Defaults keep the main glass workflow small.</summary>
    [Serializable]
    public sealed class GlassSurfaceSettings
    {
        public GlassLengthUnits LipUnits = GlassLengthUnits.PercentOfShortSide;
        [Min(0)] public float OpticalLip = 7f;
        [Range(0.01f, 5f)] public float Smoothness = 0.75f;
        public bool StabilizeInterior = true;
        [Range(1f, 4f)] public float Magnification = 1f;
        [Range(0f, 2f)] public float Transmission = 1f;
        [Range(0f, 20f)] public float LensDepth = 1f;
        public GlassBandUnits BandUnits;
        public GlassLightMode LightMode;
        [Range(0f, 1f)] public float OpposingStrength = 0.5f;
        public Vector2 PointPosition = new Vector2(0.5f, 0.8f);
        [Min(0.01f)] public float PointRadius = 0.5f;
        [Range(0f, 1f)] public float LightSpread = 0.75f;
        [Range(0f, 1f)] public float InnerBand = 0f;
        [Range(0f, 1f)] public float OuterBand = 0.12f;
        public Color LightColor = new Color(1f, 1f, 1f, 0.48f);
        public Color LipShadow = new Color(0.025f, 0.05f, 0.09f, 0.12f);
        public Color ShadowColor = Color.clear;
        [Range(0f, 32f)] public float ShadowSize = 12f;
        public Vector2 ShadowOffset = new Vector2(0f, -4f);

        public Vector2 ResolveBands(Vector2 size)
        {
            float divisor = BandUnits == GlassBandUnits.CanvasUnits ? Mathf.Max(0.0001f, ResolveLip(size)) : 1f;
            return new Vector2(Mathf.Clamp01(InnerBand / divisor), Mathf.Clamp01(OuterBand / divisor));
        }
        public float ResolveLip(Vector2 size)
        {
            float shorter = Mathf.Max(0.0001f, Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y)));
            float value = Mathf.Max(0, OpticalLip);
            if (LipUnits == GlassLengthUnits.PercentOfShortSide) value *= shorter * 0.01f;
            if (LipUnits == GlassLengthUnits.PercentOfLongSide) value *= Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)) * 0.01f;
            return Mathf.Min(value, shorter * 0.5f);
        }
    }
}
