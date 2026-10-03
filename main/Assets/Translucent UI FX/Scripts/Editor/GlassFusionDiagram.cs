using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TranslucentUIFX.Editor
{
    /// <summary>A cached composition map, not a simulation of optical rendering.</summary>
    public sealed class GlassFusionDiagram : IDisposable
    {
        private Texture2D texture;
        private int signature;
        private double lastCheck;
        private Rect bounds;
        private readonly List<(TranslucentGlassMember member, Vector2 center, int number)> markers = new List<(TranslucentGlassMember, Vector2, int)>();

        private sealed class Shape : IDisposable
        {
            public TranslucentGlassMember Member;
            public Matrix4x4 ToLocal;
            public Rect Rect;
            public float Scale, Expansion, Radius, Smoothing;
            public EdgeShape Kind;
            public GlassOperation Operation;
            public bool Sprite, Custom, Individual;
            public Vector4 Corners, Curves;
            public GlassSpriteField.Entry Field;
            public Shape(TranslucentGlassGroup group, TranslucentGlassMember member)
            {
                Member = member; Rect = member.ShapeRect; Kind = member.Shape; Operation = member.Operation;
                Radius = member.CornerRadius; Smoothing = member.CornerContinuity; Individual = member.IndividualCorners;
                Custom = member.CornerSettings != null && member.CornerSettings.Enabled && (Kind == EdgeShape.RoundedRect || Kind == EdgeShape.ContinuousRoundedRect);
                Corners = Custom ? member.CornerSettings.ResolveRadii(Rect.size) : member.CornerRadii;
                Curves = Custom ? member.CornerSettings.Exponents : Vector4.one * 2f;
                Sprite = member.ShapeSprite != null;
                ToLocal = member.transform.worldToLocalMatrix * group.transform.localToWorldMatrix;
                Matrix4x4 toGroup = group.transform.worldToLocalMatrix * member.transform.localToWorldMatrix;
                Scale = Mathf.Max(0.0001f, Mathf.Min(toGroup.MultiplyVector(Vector3.right).magnitude, toGroup.MultiplyVector(Vector3.up).magnitude));
                Expansion = member.SurfaceExpansion + group.SurfacePadding;
                if (member.ShapeSprite != null) Field = GlassSpriteField.Acquire(member.ShapeSprite, member.AlphaThreshold, (int)group.SpriteFieldResolution);
            }
            public float Distance(Vector2 point)
            {
                Vector2 local = (Vector2)ToLocal.MultiplyPoint3x4(point) - Rect.center;
                float distance;
                if (Sprite) distance = Field != null ? Field.Distance(local, Rect.size) : float.PositiveInfinity;
                else if (Custom)
                    distance = GlassShapeUtility.CustomCornersDistance(local, Rect.size, Corners, Curves);
                else distance = GlassShapeUtility.Distance(local, Rect.size, Kind, Radius, Smoothing, Individual, Corners);
                return distance * Scale - Expansion;
            }
            public void Dispose() => GlassSpriteField.Release(Field);
        }

        private static List<Shape> Capture(TranslucentGlassGroup group)
        {
            var shapes = new List<Shape>();
            try
            {
                foreach (var member in group.Members)
                    if (member != null && member.isActiveAndEnabled && member.Include && member.Owner == group) shapes.Add(new Shape(group, member));
                return shapes;
            }
            catch { foreach (var shape in shapes) shape.Dispose(); throw; }
        }
        private static float Sample(List<Shape> shapes, Vector2 point, float blend, out float removed)
        {
            float added = float.PositiveInfinity; removed = float.PositiveInfinity;
            foreach (var shape in shapes)
            {
                float distance = shape.Distance(point);
                if (shape.Operation == GlassOperation.Cutout) removed = GlassShapeUtility.SmoothUnion(removed, distance, blend);
                else added = GlassShapeUtility.SmoothUnion(added, distance, blend);
            }
            return -GlassShapeUtility.SmoothUnion(-added, removed, blend);
        }
        /// <summary>Composition-space distance, used by the diagram and editor regression checks.</summary>
        public static float SampleDistance(TranslucentGlassGroup group, Vector2 localPoint)
        {
            var shapes = Capture(group);
            try { return Sample(shapes, localPoint, group.FusionSoftness, out _); }
            finally { foreach (var shape in shapes) shape.Dispose(); }
        }

        private static int Signature(TranslucentGlassGroup group)
        {
            unchecked
            {
                int hash = group.FusionSoftness.GetHashCode();
                hash = hash * 31 + group.SurfacePadding.GetHashCode();
                hash = hash * 31 + (int)group.SpriteFieldResolution;
                hash = hash * 31 + EditorGUIUtility.isProSkin.GetHashCode();
                foreach (var member in group.Members)
                {
                    if (member == null) continue;
                    hash = hash * 31 + member.GetHashCode();
                    hash = hash * 31 + member.transform.localToWorldMatrix.GetHashCode();
                    hash = hash * 31 + member.ShapeRect.GetHashCode();
                    hash = hash * 31 + member.Include.GetHashCode();
                    hash = hash * 31 + member.isActiveAndEnabled.GetHashCode();
                    hash = hash * 31 + (int)member.Operation;
                    hash = hash * 31 + (int)member.Shape;
                    hash = hash * 31 + member.CornerRadius.GetHashCode();
                    hash = hash * 31 + member.CornerContinuity.GetHashCode();
                    hash = hash * 31 + member.CornerRadii.GetHashCode();
                    hash = hash * 31 + member.IndividualCorners.GetHashCode();
                    hash = hash * 31 + member.SurfaceExpansion.GetHashCode();
                    hash = hash * 31 + member.AlphaThreshold.GetHashCode();
                    hash = hash * 31 + JsonUtility.ToJson(member.CornerSettings).GetHashCode();
                    if (member.ShapeSprite != null) { hash = hash * 31 + member.ShapeSprite.GetHashCode(); hash = hash * 31 + (int)member.ShapeSprite.texture.updateCount; }
                }
                return hash;
            }
        }
        public void Draw(TranslucentGlassGroup group, Action repaint = null)
        {
            Rect r = GUILayoutUtility.GetRect(0f, 136f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                int next = Signature(group);
                if (texture == null || next != signature)
                {
                    if (texture == null || EditorApplication.timeSinceStartup - lastCheck > 0.1)
                    { Rebuild(group); signature = next; lastCheck = EditorApplication.timeSinceStartup; }
                    else repaint?.Invoke();
                }
            }
            GlassInspectorGUI.Rounded(r, EditorGUIUtility.isProSkin ? new Color(0.11f, 0.145f, 0.19f) : new Color(0.79f, 0.835f, 0.88f));
            Rect map = Fit(r, bounds.size);
            if (texture != null) GUI.DrawTexture(map, texture, ScaleMode.StretchToFill);
            foreach (var marker in markers)
            {
                if (marker.member == null || bounds.width <= 0f || bounds.height <= 0f) continue;
                Vector2 normalized = (marker.center - bounds.min) / bounds.size;
                Vector2 position = new Vector2(map.x + normalized.x * map.width, map.yMax - normalized.y * map.height);
                Rect hit = new Rect(position.x - 11f, position.y - 10f, 22f, 20f);
                Color ink = marker.member.Operation == GlassOperation.Cutout ? GlassInspectorGUI.CutoutColor : GlassInspectorGUI.Accent;
                GlassInspectorGUI.Rounded(hit, EditorGUIUtility.isProSkin ? new Color(0.08f, 0.12f, 0.17f, 0.92f) : new Color(0.94f, 0.97f, 1f, 0.95f), 6f);
                if (GUI.Button(hit, new GUIContent(marker.number.ToString("00"), "Edit " + marker.member.name), new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = ink } }))
                    Selection.activeGameObject = marker.member.gameObject;
            }
            GUILayout.Label("Composition map · click a number to edit its shape", new GUIStyle(EditorStyles.wordWrappedMiniLabel) { normal = { textColor = GlassInspectorGUI.Muted } });
        }

        private static Rect Fit(Rect available, Vector2 size)
        {
            float scale = Mathf.Min((available.width - 12f) / Mathf.Max(size.x, 1f), (available.height - 12f) / Mathf.Max(size.y, 1f));
            Vector2 fitted = size * scale;
            return new Rect(available.center - fitted * 0.5f, fitted);
        }
        private void Rebuild(TranslucentGlassGroup group)
        {
            var shapes = Capture(group); markers.Clear();
            try
            {
                Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity), max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                int number = 0;
                foreach (var member in group.Members)
                {
                    if (member == null) continue;
                    number++;
                    if (!member.isActiveAndEnabled || !member.Include || member.Owner != group) continue;
                    Rect rect = member.ShapeRect;
                    Matrix4x4 matrix = group.transform.worldToLocalMatrix * member.transform.localToWorldMatrix;
                    markers.Add((member, matrix.MultiplyPoint3x4(rect.center), number));
                    foreach (var corner in new[] { rect.min, rect.max, new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMax, rect.yMin) })
                    { Vector2 point = matrix.MultiplyPoint3x4(corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
                }
                if (float.IsInfinity(min.x)) { min = new Vector2(-80f, -40f); max = -min; }
                float pad = Mathf.Max(8f, group.FusionSoftness + Mathf.Max(0f, group.SurfacePadding) + 8f);
                bounds = Rect.MinMaxRect(min.x - pad, min.y - pad, max.x + pad, max.y + pad);
                // Keep a fixed pixel budget, even for extreme aspect ratios and large groups.
                int width = shapes.Count > 24 ? 160 : 240;
                int height = Mathf.Clamp(Mathf.RoundToInt(width * bounds.height / Mathf.Max(bounds.width, 1f)), 24, 120);
                if (texture == null || texture.width != width || texture.height != height)
                {
                    if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                    texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, name = "Liquid Glass composition map", filterMode = FilterMode.Bilinear };
                }
                var pixels = new Color[width * height];
                float pixelSize = Mathf.Max(bounds.width / width, bounds.height / height);
                Color surface = EditorGUIUtility.isProSkin ? new Color(0.36f, 0.66f, 0.86f) : new Color(0.21f, 0.49f, 0.73f);
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                {
                    Vector2 point = bounds.min + new Vector2((x + 0.5f) / width, (y + 0.5f) / height) * bounds.size;
                    float distance = Sample(shapes, point, group.FusionSoftness, out float cut);
                    float body = Mathf.Clamp01(0.5f - distance / pixelSize);
                    float edge = Mathf.Clamp01(1.4f - Mathf.Abs(distance) / pixelSize);
                    Color c = Color.Lerp(surface, GlassInspectorGUI.Accent, edge); c.a = Mathf.Max(body * 0.3f, edge * 0.9f);
                    float cutEdge = Mathf.Clamp01(1f - Mathf.Abs(cut) / pixelSize);
                    if (cutEdge > 0f) { c = Color.Lerp(c, GlassInspectorGUI.CutoutColor, cutEdge); c.a = Mathf.Max(c.a, cutEdge * 0.85f); }
                    pixels[y * width + x] = c;
                }
                texture.SetPixels(pixels); texture.Apply(false, false);
            }
            finally { foreach (var shape in shapes) shape.Dispose(); }
        }
        public void Dispose() { if (texture != null) UnityEngine.Object.DestroyImmediate(texture); texture = null; markers.Clear(); }
    }
}
