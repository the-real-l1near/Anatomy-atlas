using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TranslucentUIFX
{
    /// <summary>Shared, lazy sprite-alpha distance fields. Importer Read/Write is not required.</summary>
    public static class GlassSpriteField
    {
        public const int ContentSize = 256;
        public const int Padding = 4;
        public const int Resolution = ContentSize + Padding * 2;
        private static readonly Dictionary<(Sprite, int, int), Entry> Cache = new Dictionary<(Sprite, int, int), Entry>();
        private static Material s_MaskMaterial;

        public sealed class Entry
        {
            public Sprite Sprite { get; internal set; }
            public Texture2D Texture { get; internal set; }
            public Vector4 UvRow0 { get; internal set; }
            public Vector4 UvRow1 { get; internal set; }
            public float[] Distances { get; internal set; }
            internal int References;
            // Compare the source object directly; integer instance IDs are removed in Unity 6.5.
            internal Texture2D SourceTexture;
            internal uint SourceUpdate;
            public int Size { get; internal set; }
            public int Extent => Size + Padding * 2;
            internal int Threshold;
            public bool Matches(Sprite sprite, float threshold, int size) => sprite != null && sprite.texture != null && Sprite == sprite && Texture != null
                && Size == NormalizeSize(size) && Threshold == QuantizeThreshold(threshold)
                && SourceTexture == sprite.texture && SourceUpdate == sprite.texture.updateCount;
            public float Distance(Vector2 local, Vector2 size)
            {
                size = Vector2.Max(size, Vector2.one * 0.0001f);
                Vector2 canonical = local / size + Vector2.one * 0.5f;
                Vector2 bounded = new Vector2(Mathf.Clamp01(canonical.x), Mathf.Clamp01(canonical.y));
                float d = Sample(bounded);
                Vector2 stepX = new Vector2(1f / Size, 0f), stepY = new Vector2(0f, 1f / Size);
                Vector2 gradient = new Vector2(Sample(bounded + stepX) - Sample(bounded - stepX), Sample(bounded + stepY) - Sample(bounded - stepY)) * (Size * 0.5f) / size;
                return d / Mathf.Max(gradient.magnitude, 0.001f) + Vector2.Scale(canonical - bounded, size).magnitude;
            }
            public float Sample(Vector2 canonical)
            {
                Vector2 pixel = canonical * Size + Vector2.one * (Padding - 0.5f);
                int x = Mathf.Clamp(Mathf.FloorToInt(pixel.x), 0, Extent - 2);
                int y = Mathf.Clamp(Mathf.FloorToInt(pixel.y), 0, Extent - 2);
                float tx = Mathf.Clamp01(pixel.x - x), ty = Mathf.Clamp01(pixel.y - y);
                return Mathf.Lerp(Mathf.Lerp(Distances[y * Extent + x], Distances[y * Extent + x + 1], tx),
                    Mathf.Lerp(Distances[(y + 1) * Extent + x], Distances[(y + 1) * Extent + x + 1], tx), ty);
            }
        }

        private static int NormalizeSize(int size) => Mathf.Clamp(Mathf.ClosestPowerOfTwo(size), 64, 1024);
        private static int QuantizeThreshold(float threshold) => Mathf.Clamp(Mathf.RoundToInt(threshold * 255f), 1, 254);

        public static Entry Acquire(Sprite sprite, float threshold = 0.5f, int size = ContentSize)
        {
            if (sprite == null || sprite.texture == null) return null;
            var key = (sprite, QuantizeThreshold(threshold), NormalizeSize(size));
            if (Cache.TryGetValue(key, out Entry entry) && entry.Matches(sprite, threshold, size))
            {
                entry.References++;
                return entry;
            }
            entry = Build(sprite, key.Item2, key.Item3);
            if (entry == null) return null;
            entry.References = 1;
            Cache[key] = entry;
            return entry;
        }

        public static void Release(Entry entry)
        {
            if (entry == null || --entry.References > 0) return;
            if (!ReferenceEquals(entry.Sprite, null) && Cache.TryGetValue((entry.Sprite, entry.Threshold, entry.Size), out Entry current) && current == entry) Cache.Remove((entry.Sprite, entry.Threshold, entry.Size));
            Dispose(entry.Texture);
            entry.Texture = null;
            if (Cache.Count == 0) { Dispose(s_MaskMaterial); s_MaskMaterial = null; }
        }

        public static void InvalidateAll()
        {
            foreach (Entry entry in Cache.Values) { Dispose(entry.Texture); entry.Texture = null; }
            Cache.Clear();
            Dispose(s_MaskMaterial); s_MaskMaterial = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() { InvalidateAll(); }
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorCleanup() { UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += InvalidateAll; }
#endif
        private static void Dispose(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value);
        }

        private static Entry Build(Sprite sprite, int threshold, int contentSize)
        {
            int resolution = contentSize + Padding * 2;
            if (s_MaskMaterial == null)
            {
                Shader shader = Resources.Load<Shader>("GlassSpriteMask");
                if (shader == null || !shader.isSupported) return null;
                s_MaskMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }
            Vector2[] vertices = sprite.vertices, uv = sprite.uv;
            ushort[] sourceTriangles = sprite.triangles;
            if (sourceTriangles.Length < 3) return null;
            Vector3[] positions = new Vector3[vertices.Length];
            Vector2[] canonical = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                canonical[i] = (vertices[i] * sprite.pixelsPerUnit + sprite.pivot) / sprite.rect.size;
                positions[i] = (canonical[i] * contentSize + Vector2.one * Padding) / resolution;
            }
            int[] triangles = Array.ConvertAll(sourceTriangles, x => (int)x);
            int a = 0, b = 0, c = 0;
            Vector2 u = Vector2.zero, v = Vector2.zero;
            float determinant = 0f;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                a = triangles[i]; b = triangles[i + 1]; c = triangles[i + 2];
                u = uv[b] - uv[a]; v = uv[c] - uv[a];
                determinant = u.x * v.y - u.y * v.x;
                if (Mathf.Abs(determinant) > 1e-12f) break;
            }
            if (Mathf.Abs(determinant) <= 1e-12f) return null;
            Mesh mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, vertices = positions, uv = uv, triangles = triangles };
            RenderTexture render = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Texture2D alpha = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture previous = RenderTexture.active;
            Color32[] pixels;
            try
            {
                s_MaskMaterial.SetTexture("_MainTex", sprite.texture);
                using (var commands = new CommandBuffer { name = "Liquid Glass sprite silhouette" })
                {
                    commands.SetRenderTarget(render);
                    commands.ClearRenderTarget(false, true, Color.clear);
                    commands.DrawMesh(mesh, Matrix4x4.identity, s_MaskMaterial);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = render;
                alpha.ReadPixels(new Rect(0, 0, resolution, resolution), 0, 0, false);
                alpha.Apply(false, false);
                pixels = alpha.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(render);
                Dispose(mesh); Dispose(alpha);
            }
            bool[] inside = new bool[pixels.Length];
            for (int i = 0; i < inside.Length; i++) inside[i] = pixels[i].a >= threshold;
            float[] toInside = Transform(inside, true, resolution), toOutside = Transform(inside, false, resolution);
            float[] distance = new float[inside.Length];
            for (int i = 0; i < distance.Length; i++)
                distance[i] = Mathf.Clamp(inside[i] ? 0.5f - Mathf.Sqrt(toOutside[i]) : Mathf.Sqrt(toInside[i]) - 0.5f, -resolution, resolution);
            Texture2D field = new Texture2D(resolution, resolution, TextureFormat.RFloat, true, true)
            { name = sprite.name + " (glass distance field)", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            field.SetPixelData(distance, 0); field.Apply(true, false);
            // Affine atlas UV mapping also handles trimmed and rotated packed sprites.
            Vector2 dx = (canonical[b] - canonical[a]) * v.y - (canonical[c] - canonical[a]) * u.y;
            Vector2 dy = (canonical[c] - canonical[a]) * u.x - (canonical[b] - canonical[a]) * v.x;
            dx /= determinant; dy /= determinant;
            Vector2 offset = canonical[a] - dx * uv[a].x - dy * uv[a].y;
            float scale = (float)contentSize / resolution, pad = (float)Padding / resolution;
            return new Entry { Sprite = sprite, Texture = field, Distances = distance, Size = contentSize, Threshold = threshold, SourceUpdate = sprite.texture.updateCount, SourceTexture = sprite.texture,
                UvRow0 = new Vector4(dx.x * scale, dy.x * scale, offset.x * scale + pad, 0f),
                UvRow1 = new Vector4(dx.y * scale, dy.y * scale, offset.y * scale + pad, 0f) };
        }

        // The lower envelope of squared distances is separable: rows, then columns.
        // Every texel participates a constant number of times, rather than searching a radius.
        private static float[] Transform(bool[] inside, bool seedInside, int n)
        {
            float[] result = new float[n * n], line = new float[n], output = new float[n], boundaries = new float[n + 1];
            int[] centers = new int[n];
            for (int row = 0; row < n; row++)
            {
                for (int x = 0; x < n; x++) line[x] = inside[row * n + x] == seedInside ? 0f : 1e12f;
                TransformLine(line, output, centers, boundaries);
                for (int x = 0; x < n; x++) result[row * n + x] = output[x];
            }
            for (int column = 0; column < n; column++)
            {
                for (int y = 0; y < n; y++) line[y] = result[y * n + column];
                TransformLine(line, output, centers, boundaries);
                for (int y = 0; y < n; y++) result[y * n + column] = output[y];
            }
            return result;
        }
        private static void TransformLine(float[] source, float[] output, int[] centers, float[] boundaries)
        {
            int last = 0; centers[0] = 0; boundaries[0] = float.NegativeInfinity; boundaries[1] = float.PositiveInfinity;
            for (int candidate = 1; candidate < source.Length; candidate++)
            {
                float crossing;
                while (true)
                {
                    int previous = centers[last];
                    crossing = (float)(((double)source[candidate] + candidate * candidate - source[previous] - previous * previous) / (2.0 * (candidate - previous)));
                    if (crossing > boundaries[last] || last == 0) break;
                    last--;
                }
                centers[++last] = candidate; boundaries[last] = crossing; boundaries[last + 1] = float.PositiveInfinity;
            }
            int segment = 0;
            for (int pixel = 0; pixel < source.Length; pixel++)
            {
                while (boundaries[segment + 1] < pixel) segment++;
                float delta = pixel - centers[segment];
                output[pixel] = delta * delta + source[centers[segment]];
            }
        }
    }
}
