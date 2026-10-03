using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace TranslucentUIFX.Tests.Editor
{
    public sealed class GlassBlurEquivalenceTests
    {
        [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void DirectFinalBlurMatchesThePreviousCopyPath(int iterations)
        {
            var material = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/TranslucentUIFX/Blur"));
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var pixels = new Color[64 * 64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color((i % 7) / 6f, (i % 17) / 16f, (i % 31) / 30f, 1);
            texture.SetPixels(pixels); texture.Apply();
            var source = RTHandles.Alloc(texture);
            var a = RTHandles.Alloc(64, 64, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, filterMode: FilterMode.Bilinear);
            var b = RTHandles.Alloc(64, 64, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, filterMode: FilterMode.Bilinear);
            var oldOutput = RTHandles.Alloc(64, 64, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, filterMode: FilterMode.Trilinear, useMipMap: true, autoGenerateMips: false);
            var newOutput = RTHandles.Alloc(64, 64, colorFormat: GraphicsFormat.R8G8B8A8_UNorm, filterMode: FilterMode.Trilinear, useMipMap: true, autoGenerateMips: false);
            var readback = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
            var previous = RenderTexture.active;
            try
            {
                for (int path = 0; path < 2; path++)
                {
                    using var commands = new CommandBuffer();
                    Blitter.BlitCameraTexture(commands, source, a);
                    var src = a; var dst = b;
                    for (int i = 0; i < iterations; i++)
                    {
                        float offset = (i + 1f) / 64f;
                        commands.SetGlobalVector("_TranslucentBlurOffset", new Vector4(offset, offset, 0, 0));
                        Blitter.BlitCameraTexture(commands, src, path == 1 && i == iterations - 1 ? newOutput : dst, material, 0);
                        var swap = src; src = dst; dst = swap;
                    }
                    if (path == 0) Blitter.BlitCameraTexture(commands, src, oldOutput);
                    Graphics.ExecuteCommandBuffer(commands);
                }
                RenderTexture.active = oldOutput.rt; readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); readback.Apply(); var oldPixels = readback.GetPixels32();
                RenderTexture.active = newOutput.rt; readback.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); readback.Apply(); var newPixels = readback.GetPixels32();
                Assert.AreEqual(oldPixels, newPixels, "Removing the final copy must preserve all rendered bytes.");
            }
            finally
            {
                RenderTexture.active = previous;
                source.Release(); a.Release(); b.Release(); oldOutput.Release(); newOutput.Release();
                Object.DestroyImmediate(texture); Object.DestroyImmediate(material); Object.DestroyImmediate(readback);
            }
        }
    }
}
