using UnityEngine;

namespace TranslucentUIFX
{
    /// <summary>Analytic silhouettes shared by pointer hit testing and editor previews.</summary>
    public static class GlassShapeUtility
    {
        public static Vector4 ClampCorners(Vector4 corners)
        {
            for (int i = 0; i < 4; i++) corners[i] = Mathf.Clamp(corners[i], 0f, 0.5f);
            return corners;
        }

        // Corners are top-left, top-right, bottom-right, bottom-left. Units are a
        // fraction of the shorter side, independent of Canvas scale or aspect ratio.
        public static float Distance(Vector2 point, Vector2 size, EdgeShape shape, float radius,
            float smoothing, bool individualCorners = false, Vector4 corners = default)
        {
            Vector2 half = new Vector2(Mathf.Max(size.x, 0.001f), Mathf.Max(size.y, 0.001f)) * 0.5f;
            float shortHalf = Mathf.Min(half.x, half.y);
            if (shape == EdgeShape.Circle) return point.magnitude - shortHalf;
            if (shape == EdgeShape.Triangle || shape == EdgeShape.Hexagon)
                return PolygonDistance(point, half, shape == EdgeShape.Triangle ? 3 : 6);
            if (shape == EdgeShape.Diamond)
                return (Mathf.Abs(point.x) / half.x + Mathf.Abs(point.y) / half.y - 1f) * shortHalf * 0.70710678f;
            if (individualCorners)
                radius = corners[point.y >= 0f ? (point.x < 0f ? 0 : 1) : (point.x < 0f ? 3 : 2)];
            float r = shape == EdgeShape.Rectangle ? 0f : shape == EdgeShape.Capsule ? shortHalf : Mathf.Clamp01(radius * 2f) * shortHalf;
            Vector2 q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - half + Vector2.one * r;
            Vector2 outside = Vector2.Max(q, Vector2.zero);
            float exponent = shape == EdgeShape.ContinuousRoundedRect ? Mathf.Lerp(2f, 4f, Mathf.Clamp01(smoothing)) : 2f;
            float curved = Mathf.Pow(Mathf.Pow(outside.x, exponent) + Mathf.Pow(outside.y, exponent), 1f / exponent);
            return curved + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        public static float CustomCornersDistance(Vector2 point, Vector2 size, Vector4 radii, Vector4 exponents)
        {
            Vector2 half = Vector2.Max(size * 0.5f, Vector2.one * 0.0001f);
            Vector2 q = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y)) - half;
            float distance = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f);
            for (int i = 0; i < 4; i++)
            {
                float radius = radii[i]; if (radius < 0.0001f) continue;
                Vector2 sign = new Vector2(i == 0 || i == 3 ? -1 : 1, i < 2 ? 1 : -1);
                Vector2 local = Vector2.Scale(point, sign) - half + Vector2.one * radius;
                if (local.x <= 0f || local.y <= 0f) continue;
                Vector2 u = Vector2.Max(local / radius, Vector2.one * 0.00001f);
                float power = Mathf.Clamp(exponents[i], 0.5f, 16f);
                float sum = Mathf.Pow(u.x, power) + Mathf.Pow(u.y, power);
                float norm = Mathf.Pow(sum, 1f / power);
                Vector2 gradient = new Vector2(Mathf.Pow(u.x, power - 1f), Mathf.Pow(u.y, power - 1f)) * Mathf.Pow(sum, 1f / power - 1f);
                distance = Mathf.Max(distance, (norm - 1f) * radius / Mathf.Max(gradient.magnitude, 0.001f));
            }
            return distance;
        }

        private static float PolygonDistance(Vector2 point, Vector2 half, int count)
        {
            float distanceSquared = float.PositiveInfinity;
            bool inside = true;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = PolygonVertex(i, count, half);
                Vector2 b = PolygonVertex((i + 1) % count, count, half);
                Vector2 edge = b - a;
                Vector2 relative = point - a;
                Vector2 closest = relative - edge * Mathf.Clamp01(Vector2.Dot(relative, edge) / Mathf.Max(edge.sqrMagnitude, 0.000001f));
                distanceSquared = Mathf.Min(distanceSquared, closest.sqrMagnitude);
                inside &= edge.x * relative.y - edge.y * relative.x >= 0f;
            }
            return Mathf.Sqrt(distanceSquared) * (inside ? -1f : 1f);
        }

        private static Vector2 PolygonVertex(int i, int count, Vector2 half)
        {
            if (count == 3)
                return i == 0 ? new Vector2(0f, half.y) : i == 1 ? new Vector2(-half.x, -half.y) : new Vector2(half.x, -half.y);
            float angle = i * Mathf.PI / 3f;
            return new Vector2(Mathf.Cos(angle) * half.x, Mathf.Sin(angle) * half.y / 0.8660254f);
        }

        public static float SmoothUnion(float a, float b, float distance)
        {
            if (distance <= 0.00001f) return Mathf.Min(a, b);
            float overlap = Mathf.Max(distance - Mathf.Abs(a - b), 0f) / distance;
            return Mathf.Min(a, b) - overlap * overlap * distance * 0.25f;
        }
    }
}
