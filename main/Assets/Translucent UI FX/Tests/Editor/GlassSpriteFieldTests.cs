using NUnit.Framework;
using UnityEngine;

namespace TranslucentUIFX.Tests.Editor
{
    public sealed class GlassSpriteFieldTests
    {
        private Texture2D texture;
        private Sprite sprite;
        private GlassSpriteField.Entry field;
        [TearDown] public void Cleanup()
        {
            GlassSpriteField.Release(field); field = null;
            if (sprite != null) Object.DestroyImmediate(sprite);
            if (texture != null) Object.DestroyImmediate(texture);
        }
        private void Create(bool readable = false, Rect? rectangle = null)
        {
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[64 * 64];
            // Asymmetric L: detects flipped readback and incorrect UV/atlas coordinates.
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                    pixels[y * 64 + x] = new Color32(255, 255, 255, (byte)(x < 20 || y < 20 ? 255 : 0));
            texture.SetPixels32(pixels); texture.Apply(false, !readable);
            sprite = Sprite.Create(texture, rectangle ?? new Rect(0, 0, 64, 64), new Vector2(0.3f, 0.7f), 100, 0, SpriteMeshType.FullRect);
            field = GlassSpriteField.Acquire(sprite);
            Assert.That(field, Is.Not.Null);
        }
        [Test] public void NonReadableSpriteHasCorrectOrientationAndBoundary()
        {
            Create();
            Assert.That(texture.isReadable, Is.False);
            Assert.That(field.Sample(new Vector2(0.15f, 0.85f)), Is.LessThan(0));
            Assert.That(field.Sample(new Vector2(0.85f, 0.15f)), Is.LessThan(0));
            Assert.That(field.Sample(new Vector2(0.85f, 0.85f)), Is.GreaterThan(0));
            Assert.That(field.Sample(new Vector2(0.31f, 0.7f)), Is.EqualTo(0).Within(2));
        }
        [Test] public void SpriteRectAndPivotMapBackToCanonicalCoordinates()
        {
            Create(false, new Rect(8, 8, 40, 48));
            Vector2 uv = new Vector2(12f / 64, 45f / 64);
            Vector3 source = new Vector3(uv.x, uv.y, 1);
            Vector2 fieldUv = new Vector2(Vector3.Dot(field.UvRow0, source), Vector3.Dot(field.UvRow1, source));
            Vector2 canonical = (fieldUv * GlassSpriteField.Resolution - Vector2.one * GlassSpriteField.Padding) / GlassSpriteField.ContentSize;
            Assert.That(canonical.x, Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(canonical.y, Is.EqualTo(37f / 48).Within(0.001f));
            Assert.That(field.Sample(canonical), Is.LessThan(0));
            Assert.That(field.Sample(new Vector2(0.8f, 0.8f)), Is.GreaterThan(0));
        }
        [Test] public void FieldsAreSharedAndReleasedAfterLastConsumer()
        {
            Create();
            var second = GlassSpriteField.Acquire(sprite);
            Assert.That(second, Is.SameAs(field));
            GlassSpriteField.Release(second);
            Assert.That(field.Texture, Is.Not.Null);
            var released = field;
            GlassSpriteField.Release(field); field = null;
            Assert.That(released.Texture == null, Is.True);
        }
        [Test] public void AssignedSpriteOverridesLegacyProceduralFlagAndCanBeCleared()
        {
            Create();
            var go = new GameObject("Sprite glass test", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                var glass = go.AddComponent<TranslucentImageFX>();
                glass.ProceduralShape = true; glass.sprite = sprite;
                Assert.That(glass.UsesSpriteSilhouette, Is.True);
                Assert.That(glass.materialForRendering.GetFloat("_SpriteShape"), Is.EqualTo(1));
                Assert.That(glass.materialForRendering.GetFloat("_ProceduralShape"), Is.Zero);
                Assert.That(glass.materialForRendering.GetTexture("_SpriteDistanceTex"), Is.SameAs(field.Texture));
                glass.sprite = null;
                Assert.That(glass.UsesSpriteSilhouette, Is.False);
                Assert.That(glass.materialForRendering.GetFloat("_SpriteShape"), Is.Zero);
                Assert.That(glass.materialForRendering.GetFloat("_ProceduralShape"), Is.EqualTo(1));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void FilledSpriteRejectsPointerInputOutsideItsVisibleFill()
        {
            Create(true);
            var pixels = new Color32[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels); texture.Apply(false, true);
            var go = new GameObject("Filled sprite hit test", typeof(RectTransform), typeof(CanvasRenderer));
            try
            {
                var glass = go.AddComponent<TranslucentImageFX>();
                glass.rectTransform.sizeDelta = Vector2.one * 100;
                glass.sprite = sprite; glass.type = UnityEngine.UI.Image.Type.Filled;
                glass.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal; glass.fillOrigin = 0; glass.fillAmount = 0.25f;
                Vector2 visible = RectTransformUtility.WorldToScreenPoint(null, go.transform.TransformPoint(new Vector2(-40, 0)));
                Vector2 empty = RectTransformUtility.WorldToScreenPoint(null, go.transform.TransformPoint(new Vector2(40, 0)));
                Assert.IsTrue(glass.IsRaycastLocationValid(visible, null));
                Assert.IsFalse(glass.IsRaycastLocationValid(empty, null));
                glass.fillAmount = 0f;
                Assert.IsFalse(glass.IsRaycastLocationValid(visible, null));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test] public void ThresholdAndResolutionHaveSeparateCacheEntries()
        {
            Create(true);
            var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) if (pixels[i].a > 0) pixels[i].a = 100;
            texture.SetPixels32(pixels); texture.Apply();
            var low = GlassSpriteField.Acquire(sprite, 0.2f, 64);
            var high = GlassSpriteField.Acquire(sprite, 0.8f, 128);
            try
            {
                Assert.AreEqual(72, low.Texture.width);
                Assert.AreEqual(136, high.Texture.width);
                Assert.Less(low.Sample(new Vector2(0.1f, 0.1f)), 0);
                Assert.Greater(high.Sample(new Vector2(0.1f, 0.1f)), 0);
                Assert.IsFalse(field.Matches(sprite, 0.5f, 256), "Texture updates must invalidate an existing field.");
            }
            finally { GlassSpriteField.Release(low); GlassSpriteField.Release(high); }
        }

        [Test] public void InvalidationRebuildsChangedSpriteAlpha()
        {
            Create(true);
            var pixels = new Color32[64 * 64];
            texture.SetPixels32(pixels); texture.Apply();
            GlassSpriteField.InvalidateAll();
            Assert.That(field.Texture == null, Is.True);
            GlassSpriteField.Release(field);
            field = GlassSpriteField.Acquire(sprite);
            Assert.That(field.Sample(new Vector2(0.1f, 0.1f)), Is.GreaterThan(0));
        }
    }
}
