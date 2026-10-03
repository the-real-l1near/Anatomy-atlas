using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace TranslucentUIFX
{
    public class TranslucentRenderPass : ScriptableRenderPass
    {
        private TranslucentRendererFeature.TranslucentSettings m_Settings;
        private Material m_BlurMaterial;
        private ProfilingSampler m_ProfilingSampler = new ProfilingSampler("Translucent UI FX Pass");

        private class CameraDataStorage
        {
            public RTHandle PersistentTemp0;
            public RTHandle PersistentTemp1;
            public RTHandle PersistentSourceTexture;
            public RTHandle PersistentBlurredTexture;
            public Vector3 LastPos;
            public Quaternion LastRot;
            public Matrix4x4 LastProjectionMatrix;
            public Rect LastPixelRect;
            public bool RequiresUpdate;
            public int LastProcessedRequestVersion;
            public float PersistentMaxBlur;

            public void Dispose()
            {
                PersistentTemp0?.Release();
                PersistentTemp1?.Release();
                PersistentSourceTexture?.Release();
                PersistentBlurredTexture?.Release();
            }
        }

        private Dictionary<Camera, CameraDataStorage> m_CameraData = new Dictionary<Camera, CameraDataStorage>();
        private readonly List<Camera> m_DeadCameras = new List<Camera>();
        private int m_LastCameraCleanupFrame = -120;

        private static readonly int s_TranslucentBlurredTextureID = Shader.PropertyToID("_TranslucentUI_BlurredTex");
        private static readonly int s_TranslucentSourceTextureID = Shader.PropertyToID("_TranslucentUI_SourceTex");
        private static readonly int s_TranslucentMaxBlurID = Shader.PropertyToID("_TranslucentUI_MaxBlurStrength");

        public TranslucentRenderPass(TranslucentRendererFeature.TranslucentSettings settings, Material blurMaterial)
        {
            m_Settings = settings;
            m_BlurMaterial = blurMaterial;
            this.renderPassEvent = (RenderPassEvent)settings.injectionPoint;
            requiresIntermediateTexture = true;
        }

        private void GetDynamicSettings(Camera camera, out TranslucentUpdateMode activeMode, out int activeInterval)
        {
            // Start with backend fallbacks
            activeMode = m_Settings.updateMode;
            activeInterval = m_Settings.updateIntervalFrames;

            if (TranslucentImageFX.ActiveInstances.Count == 0) return;

            activeMode = TranslucentUpdateMode.Manual; // Start at lowest priority
            activeInterval = 120; // Highest interval

            for (int i = TranslucentImageFX.ActiveInstances.Count - 1; i >= 0; i--)
            {
                var instance = TranslucentImageFX.ActiveInstances[i];
                if (instance == null || !instance.isActiveAndEnabled)
                {
                    TranslucentImageFX.ActiveInstances.RemoveAt(i);
                    continue;
                }

                if (!instance.NeedsBackground || !instance.MatchesCapture(camera, m_Settings.captureLayer)) continue;
                if (GetUpdatePriority(instance.UpdateMode) < GetUpdatePriority(activeMode))
                    activeMode = instance.UpdateMode;

                if (instance.UpdateMode == TranslucentUpdateMode.Interval && instance.UpdateInterval < activeInterval)
                    activeInterval = instance.UpdateInterval;
            }
        }

        private static int GetUpdatePriority(TranslucentUpdateMode mode)
        {
            // Interval must outrank Smart Update when both are present: a stationary camera
            // should not prevent an interval-authored element from receiving its refreshes.
            if (mode == TranslucentUpdateMode.Always) return 0;
            if (mode == TranslucentUpdateMode.Interval) return 1;
            if (mode == TranslucentUpdateMode.SmartUpdate) return 2;
            return 3;
        }

        private bool NeedsUpdate(Camera camera, CameraDataStorage data)
        {
            if (data.PersistentBlurredTexture == null) return true;
            if (data.RequiresUpdate) return true;
            if (TranslucentRendererFeature.HasUpdateRequestSince(data.LastProcessedRequestVersion)) return true;

            GetDynamicSettings(camera, out var activeMode, out var activeInterval);

            switch (activeMode)
            {
                case TranslucentUpdateMode.Always:
                    return true;
                
                case TranslucentUpdateMode.Interval:
                    return Time.frameCount % Mathf.Max(1, activeInterval) == 0;
                
                case TranslucentUpdateMode.Manual:
                    return TranslucentRendererFeature.HasUpdateRequestSince(data.LastProcessedRequestVersion);

                case TranslucentUpdateMode.SmartUpdate:
                    bool changed = data.LastPos != camera.transform.position ||
                                   data.LastRot != camera.transform.rotation ||
                                   data.LastProjectionMatrix != camera.projectionMatrix ||
                                   data.LastPixelRect != camera.pixelRect;
                    if (changed)
                    {
                        data.LastPos = camera.transform.position;
                        data.LastRot = camera.transform.rotation;
                        data.LastProjectionMatrix = camera.projectionMatrix;
                        data.LastPixelRect = camera.pixelRect;
                    }
                    return changed;
            }
            return true;
        }

        private int CalculateDownsample(PerformanceMode reqQuality)
        {
            return reqQuality == PerformanceMode.High ? 2 : 
                   reqQuality == PerformanceMode.Medium ? 3 : 4;
        }

        public void Dispose()
        {
            foreach (var kvp in m_CameraData)
            {
                GlassCaptureRegistry.Remove(kvp.Key, m_Settings.captureLayer, this);
                kvp.Value.Dispose();
            }
            m_CameraData.Clear();
            m_DeadCameras.Clear();
        }

        private void CleanupDestroyedCameras()
        {
            if (m_CameraData.Count == 0 || Time.frameCount - m_LastCameraCleanupFrame < 120) return;
            m_LastCameraCleanupFrame = Time.frameCount;

            foreach (var pair in m_CameraData)
            {
                if (pair.Key != null) continue;
                GlassCaptureRegistry.Remove(pair.Key, m_Settings.captureLayer, this);
                pair.Value.Dispose();
                m_DeadCameras.Add(pair.Key);
            }

            foreach (Camera deadCamera in m_DeadCameras)
                m_CameraData.Remove(deadCamera);

            m_DeadCameras.Clear();
        }

        // --- RENDERGRAPH PIPELINE ---
        private class PassData
        {
            public TextureHandle srcCam;
            public TextureHandle temp0;
            public TextureHandle temp1;
            public TextureHandle sourceTex;
            public TextureHandle blurredTex;
            public Material blurMaterial;
            public int iterations;
            public float maxBlur;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (m_BlurMaterial == null || TranslucentImageFX.ActiveInstances.Count == 0) return;
            CleanupDestroyedCameras();

            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            int consumers = 0;
            var reqQuality = PerformanceMode.Low;
            float maxBlur = 0f;
            for (int i = TranslucentImageFX.ActiveInstances.Count - 1; i >= 0; i--) {
                var instance = TranslucentImageFX.ActiveInstances[i];
                if (instance == null || !instance.isActiveAndEnabled)
                {
                    TranslucentImageFX.ActiveInstances.RemoveAt(i);
                    continue;
                }

                if (!instance.NeedsBackground || !instance.MatchesCapture(cameraData.camera, m_Settings.captureLayer)) continue;
                consumers++;
                if (instance.QualityMode > reqQuality) reqQuality = instance.QualityMode;
                // Keep the shared blur authored at full strength so Glass Intensity can animate
                // entirely in the UI shader, including while Manual mode freezes the capture.
                float requestedBlur = instance.RequiredBlurStrength;
                if (requestedBlur > maxBlur) maxBlur = requestedBlur;
            }

            if (consumers == 0) return;
            var cameraOverride = cameraData.camera.GetComponent<GlassCameraOverride>();
            bool overrideActive = cameraOverride != null && cameraOverride.isActiveAndEnabled;
            if (overrideActive && cameraOverride.OverrideQuality) reqQuality = cameraOverride.Quality;
            float kernel = overrideActive && cameraOverride.OverrideBlur ? cameraOverride.KernelSpread : m_Settings.kernelSpread;
            float dither = overrideActive && cameraOverride.OverrideBlur ? cameraOverride.Dither : m_Settings.dither;

            int downsample = CalculateDownsample(reqQuality);
            int iterations = reqQuality == PerformanceMode.High ? 4 :
                             reqQuality == PerformanceMode.Medium ? 3 : 2;

            iterations = overrideActive && cameraOverride.OverrideBlur ? cameraOverride.BlurPasses : Mathf.Min(iterations, m_Settings.blurPasses);
            if (iterations == 0) maxBlur = 0f;

            RenderTextureDescriptor sourceDesc = cameraData.cameraTargetDescriptor;
            sourceDesc.depthBufferBits = 0;
            sourceDesc.msaaSamples = 1;
            sourceDesc.useMipMap = m_Settings.backdropMipLevels > 0;
            sourceDesc.autoGenerateMips = false;
            sourceDesc.mipCount = m_Settings.backdropMipLevels > 0 ? Mathf.Min(m_Settings.backdropMipLevels + 1, 1 + Mathf.FloorToInt(Mathf.Log(Mathf.Max(1, Mathf.Min(sourceDesc.width, sourceDesc.height)), 2f))) : 1;
            if (!m_Settings.matchCameraFormat &&
                SystemInfo.IsFormatSupported(m_Settings.captureFormat, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.Render) &&
                SystemInfo.IsFormatSupported(m_Settings.captureFormat, UnityEngine.Experimental.Rendering.GraphicsFormatUsage.Sample))
                sourceDesc.graphicsFormat = m_Settings.captureFormat;

            // High quality retains a full-resolution source for crisp refraction and dispersion.
            // Medium/Low share a 2x source to cap bandwidth on mobile-oriented profiles.
            int sourceDownsample = reqQuality == PerformanceMode.High ? 1 : 2;
            sourceDesc.width = Mathf.Max(1, sourceDesc.width / sourceDownsample);
            sourceDesc.height = Mathf.Max(1, sourceDesc.height / sourceDownsample);

            RenderTextureDescriptor desc = sourceDesc;
            desc.width = Mathf.Max(1, cameraData.cameraTargetDescriptor.width / downsample);
            desc.height = Mathf.Max(1, cameraData.cameraTargetDescriptor.height / downsample);

            sourceDesc.mipCount = Mathf.Min(sourceDesc.mipCount, 1 + Mathf.FloorToInt(Mathf.Log(Mathf.Max(1, Mathf.Min(sourceDesc.width, sourceDesc.height)), 2f)));
            desc.mipCount = Mathf.Min(desc.mipCount, 1 + Mathf.FloorToInt(Mathf.Log(Mathf.Max(1, Mathf.Min(desc.width, desc.height)), 2f)));
            if (!m_CameraData.TryGetValue(cameraData.camera, out var camData))
            {
                camData = new CameraDataStorage();
                m_CameraData[cameraData.camera] = camData;
            }

            RenderTextureDescriptor temporaryDesc = desc; temporaryDesc.useMipMap = false; temporaryDesc.mipCount = 1;
            bool alloc0 = RenderingUtils.ReAllocateHandleIfNeeded(ref camData.PersistentTemp0, temporaryDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempBlur0");
            bool alloc1 = RenderingUtils.ReAllocateHandleIfNeeded(ref camData.PersistentTemp1, temporaryDesc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TempBlur1");
            bool allocSource = RenderingUtils.ReAllocateHandleIfNeeded(ref camData.PersistentSourceTexture, sourceDesc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: "_TranslucentUI_PersistentSource");
            bool allocTex = RenderingUtils.ReAllocateHandleIfNeeded(ref camData.PersistentBlurredTexture, desc, FilterMode.Trilinear, TextureWrapMode.Clamp, name: "_TranslucentUI_PersistentBlur");

            if (alloc0 || alloc1 || allocSource || allocTex)
            {
                camData.RequiresUpdate = true;
            }

            bool refresh = NeedsUpdate(cameraData.camera, camData);
            GlassCaptureRegistry.Publish(cameraData.camera, m_Settings.captureLayer, this, camData.PersistentSourceTexture.rt, camData.PersistentBlurredTexture.rt, refresh ? maxBlur : camData.PersistentMaxBlur, dither);

            if (!refresh)
            {
                TextureHandle blurredTexHandle = renderGraph.ImportTexture(camData.PersistentBlurredTexture);
                TextureHandle sourceTexHandle = renderGraph.ImportTexture(camData.PersistentSourceTexture);
                
                using (var builder = renderGraph.AddUnsafePass<PassData>("Translucent UI FX - Skip Update", out var passData))
                {
                    builder.UseTexture(blurredTexHandle, AccessFlags.Read);
                    builder.UseTexture(sourceTexHandle, AccessFlags.Read);
                    passData.blurredTex = blurredTexHandle;
                    passData.sourceTex = sourceTexHandle;
                    passData.maxBlur = camData.PersistentMaxBlur;
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        cmd.SetGlobalTexture(s_TranslucentSourceTextureID, data.sourceTex);
                        cmd.SetGlobalTexture(s_TranslucentBlurredTextureID, data.blurredTex);
                        cmd.SetGlobalFloat(s_TranslucentMaxBlurID, data.maxBlur);
                    });
                }
                return;
            }

            camData.RequiresUpdate = false;
            camData.LastProcessedRequestVersion = TranslucentRendererFeature.UpdateRequestVersion;
            camData.PersistentMaxBlur = maxBlur;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle srcCam = resourceData.activeColorTexture;
            if (!srcCam.IsValid()) return;

            TextureHandle t0 = renderGraph.ImportTexture(camData.PersistentTemp0);
            TextureHandle t1 = renderGraph.ImportTexture(camData.PersistentTemp1);
            TextureHandle sourceTex = renderGraph.ImportTexture(camData.PersistentSourceTexture);
            TextureHandle finalTex = renderGraph.ImportTexture(camData.PersistentBlurredTexture);

            using (var builder = renderGraph.AddUnsafePass<PassData>("Translucent UI FX - RenderGraph", out var passData, m_ProfilingSampler))
            {
                passData.blurMaterial = m_BlurMaterial;
                passData.iterations = iterations;
                passData.maxBlur = maxBlur;

                builder.UseTexture(srcCam, AccessFlags.Read);
                builder.UseTexture(t0, AccessFlags.ReadWrite);
                builder.UseTexture(t1, AccessFlags.ReadWrite);
                builder.UseTexture(sourceTex, AccessFlags.ReadWrite);
                builder.UseTexture(finalTex, AccessFlags.Write);

                passData.srcCam = srcCam;
                passData.temp0 = t0;
                passData.temp1 = t1;
                passData.sourceTex = sourceTex;
                passData.blurredTex = finalTex;

                builder.AllowPassCulling(false); // Force it to run and write to the global shader propert

                builder.SetRenderFunc((PassData data, UnsafeGraphContext context) =>
                {
                    CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

                    // 1. Preserve an unblurred downsample for true per-element blur control.
                    Blitter.BlitCameraTexture(cmd, data.srcCam, data.sourceTex);
                    if (sourceDesc.useMipMap) cmd.GenerateMips(camData.PersistentSourceTexture.rt);

                    if (data.maxBlur <= 0.0001f)
                    {
                        Blitter.BlitCameraTexture(cmd, data.sourceTex, data.blurredTex);
                        if (desc.useMipMap) cmd.GenerateMips(camData.PersistentBlurredTexture.rt);
                        cmd.SetGlobalTexture(s_TranslucentSourceTextureID, data.sourceTex);
                        cmd.SetGlobalTexture(s_TranslucentBlurredTextureID, data.blurredTex);
                        cmd.SetGlobalFloat(s_TranslucentMaxBlurID, 0f);
                        return;
                    }

                    Blitter.BlitCameraTexture(cmd, data.sourceTex, data.temp0);

                    var src = data.temp0;
                    var dst = data.temp1;
                    
                    float baseOffset = data.maxBlur * 1.5f * kernel;

                    // 2. Iterative blur
                    for (int i = 0; i < data.iterations; i++)
                    {
                        float currentOffset = (i + 1f) * baseOffset;
                        cmd.SetGlobalVector("_TranslucentBlurOffset", new Vector4(currentOffset / desc.width, currentOffset / desc.height, 0, 0));
                        // The final blur pass writes directly to its retained destination.
                        // Same shader, samples, resolution and format; one redundant copy removed.
                        var output = i == data.iterations - 1 ? data.blurredTex : dst;
                        Blitter.BlitCameraTexture(cmd, src, output, data.blurMaterial, 0);

                        var temp = src;
                        src = dst;
                        dst = temp;
                    }

                    // 3. Output to Final Texture
                    if (desc.useMipMap) cmd.GenerateMips(camData.PersistentBlurredTexture.rt);

                    // 4. Expose to UI components
                    cmd.SetGlobalTexture(s_TranslucentSourceTextureID, data.sourceTex);
                    cmd.SetGlobalTexture(s_TranslucentBlurredTextureID, data.blurredTex);
                    cmd.SetGlobalFloat(s_TranslucentMaxBlurID, data.maxBlur);
                });
            }
        }
        // Unity 6 RenderGraph natively manages execution; obsolete code stripped.
    }
}
