using System.Collections.Generic;
using UnityEngine;

namespace TranslucentUIFX
{
    /// <summary>Routes retained camera captures to materials without changing global camera ordering.</summary>
    internal static class GlassCaptureRegistry
    {
        internal sealed class Capture
        {
            internal object Owner;
            internal Texture Source, Blur;
            internal float MaxBlur, Dither;
        }
        private static readonly Dictionary<(Camera, int), Capture> Captures = new Dictionary<(Camera, int), Capture>();
        internal static bool TryGet(Camera camera, int layer, out Capture capture) => Captures.TryGetValue((camera, layer), out capture);
        internal static void Publish(Camera camera, int layer, object owner, Texture source, Texture blur, float maxBlur, float dither)
        {
            if (!Captures.TryGetValue((camera, layer), out var capture)) Captures[(camera, layer)] = capture = new Capture();
            bool changed = !ReferenceEquals(capture.Owner, owner) || capture.Source != source || capture.Blur != blur || capture.MaxBlur != maxBlur || capture.Dither != dither;
            capture.Owner = owner; capture.Source = source; capture.Blur = blur; capture.MaxBlur = maxBlur; capture.Dither = dither;
            if (!changed) return;
            foreach (var glass in TranslucentImageFX.ActiveInstances)
                if (glass != null && glass.MatchesCapture(camera, layer)) glass.RefreshCaptureBinding();
        }
        internal static void Remove(Camera camera, int layer, object owner)
        {
            if (!Captures.TryGetValue((camera, layer), out var capture) || !ReferenceEquals(capture.Owner, owner)) return;
            Captures.Remove((camera, layer));
            foreach (var glass in TranslucentImageFX.ActiveInstances) if (glass != null) glass.RefreshCaptureBinding();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Captures.Clear();
    }
}
