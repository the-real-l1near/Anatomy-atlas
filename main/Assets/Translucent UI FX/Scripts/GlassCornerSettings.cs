using System;
using UnityEngine;

namespace TranslucentUIFX
{
    [Serializable]
    public sealed class GlassCornerSettings
    {
        public bool Enabled;
        public GlassLengthUnits Units = GlassLengthUnits.CanvasUnits;
        public bool Normalize = true;
        public bool Continuous = true;
        public Vector4 Radii = Vector4.one * 28f;
        public Vector4 Curves = Vector4.one * 0.4f;
        public Vector4 ResolveRadii(Vector2 size)
        {
            size = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
            float units = Units == GlassLengthUnits.CanvasUnits ? 1f : (Units == GlassLengthUnits.PercentOfShortSide ? Mathf.Min(size.x, size.y) : Mathf.Max(size.x, size.y)) * 0.01f;
            Vector4 value = Vector4.Max(Radii, Vector4.zero) * units;
            for (int i = 0; i < 4; i++) value[i] = Mathf.Min(value[i], Mathf.Min(size.x, size.y));
            if (Normalize)
            {
                float scale = Mathf.Min(1f, size.x / Mathf.Max(0.0001f, value.x + value.y), size.x / Mathf.Max(0.0001f, value.z + value.w),
                    size.y / Mathf.Max(0.0001f, value.x + value.w), size.y / Mathf.Max(0.0001f, value.y + value.z));
                value *= scale;
            }
            return value;
        }
        public Vector4 Exponents
        {
            get
            {
                Vector4 result = Vector4.zero;
                for (int i = 0; i < 4; i++) result[i] = Continuous ? Mathf.Lerp(2f, 16f, Mathf.Clamp01(Curves[i])) : Mathf.Pow(2f, 1f - Mathf.Clamp(Curves[i], 0f, 2f));
                return result;
            }
        }
    }
}
