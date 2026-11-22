using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class FirstPersonHandsRendererFeature : ScriptableRendererFeature
{
    private const string LayerName = "FirstPersonHands";
    private HandsPass pass;

    internal static void SetHandsLayer(GameObject model)
    {
        int layer = LayerMask.NameToLayer(LayerName);
        if (layer < 0) return;

        foreach (var part in model.GetComponentsInChildren<Transform>(true))
            part.gameObject.layer = layer;

        foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.allowOcclusionWhenDynamic = false;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    public override void Create()
    {
        pass = new HandsPass(LayerMask.GetMask(LayerName));
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var camera = renderingData.cameraData.camera;
        if (!Application.isPlaying || camera.cameraType != CameraType.Game) return;

        var player = camera.GetComponentInParent<FirstPersonPlayer>();
        var smoking = camera.GetComponentInParent<SmokingController>();
        if ((player != null && player.PlayerCamera == camera) ||
            (smoking != null && smoking.UsesCamera(camera)))
            renderer.EnqueuePass(pass);
    }

    private sealed class HandsPass : ScriptableRenderPass
    {
        private static readonly GlobalKeyword ScreenSpaceOcclusion =
            GlobalKeyword.Create(ShaderKeywordStrings.ScreenSpaceOcclusion);

        private static readonly List<ShaderTagId> ForwardPasses = new List<ShaderTagId>
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        private readonly int layerMask;

        internal HandsPass(int layerMask)
        {
            this.layerMask = layerMask;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            // Forward renderers can otherwise target a backbuffer without a depth descriptor.
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var color = resources.activeColorTexture;
            var worldDepth = resources.activeDepthTexture;
            if (!color.IsValid() || !worldDepth.IsValid()) return;

            // Keep the world depth intact while letting the models occlude their own parts.
            var depthDescriptor = worldDepth.GetDescriptor(renderGraph);
            depthDescriptor.name = "First Person Hands Depth";
            depthDescriptor.clearBuffer = true;
            var handsDepth = renderGraph.CreateTexture(depthDescriptor);

            var cameraData = frameData.Get<UniversalCameraData>();
            var renderingData = frameData.Get<UniversalRenderingData>();
            var lightData = frameData.Get<UniversalLightData>();
            var opaque = RenderingUtils.CreateDrawingSettings(ForwardPasses, renderingData,
                cameraData, lightData, cameraData.defaultOpaqueSortFlags);
            var transparent = RenderingUtils.CreateDrawingSettings(ForwardPasses, renderingData,
                cameraData, lightData, SortingCriteria.CommonTransparent);

            using (var builder = renderGraph.AddRasterRenderPass<PassData>("First Person Hands", out var data))
            {
                data.Opaque = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults,
                    opaque, new FilteringSettings(RenderQueueRange.opaque, layerMask)));
                data.Transparent = renderGraph.CreateRendererList(new RendererListParams(renderingData.cullResults,
                    transparent, new FilteringSettings(RenderQueueRange.transparent, layerMask)));
                builder.UseRendererList(data.Opaque);
                builder.UseRendererList(data.Transparent);
                builder.UseAllGlobalTextures(true);
                data.WorldUsesSsao = resources.ssaoTexture.IsValid();
                if (data.WorldUsesSsao) builder.AllowGlobalStateModification(true);
                builder.SetRenderAttachment(color, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(handsDepth, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                {
                    // The scene's AO texture describes geometry outside the hands' depth buffer.
                    if (passData.WorldUsesSsao) context.cmd.SetKeyword(ScreenSpaceOcclusion, false);
                    context.cmd.DrawRendererList(passData.Opaque);
                    context.cmd.DrawRendererList(passData.Transparent);
                    if (passData.WorldUsesSsao) context.cmd.SetKeyword(ScreenSpaceOcclusion, true);
                });
            }
        }

        private sealed class PassData
        {
            internal RendererListHandle Opaque;
            internal RendererListHandle Transparent;
            internal bool WorldUsesSsao;
        }
    }
}
