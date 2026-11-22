using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public sealed class InteractionHintsRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;

    private Material material;
    private HintsPass pass;

    public override void Create()
    {
        CoreUtils.Destroy(material);
        material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
        pass = new HintsPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (!Application.isPlaying || material == null || camera.cameraType != CameraType.Game) return;

        var hints = camera.GetComponentInParent<InteractionHints>();
        if (hints == null || !hints.CanShow || hints.View != camera || hints.Draws.Count == 0) return;
        pass.Setup(hints, material);
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing) => CoreUtils.Destroy(material);

    private sealed class HintsPass : ScriptableRenderPass
    {
        private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        private static readonly int BlitTextureTexelSize = Shader.PropertyToID("_BlitTexture_TexelSize");
        private static readonly int OutlineColor = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
        private static readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();

        private InteractionHints hints;
        private Material material;

        internal HintsPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
            requiresIntermediateTexture = true;
        }

        internal void Setup(InteractionHints source, Material sharedMaterial)
        {
            hints = source;
            material = sharedMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var depth = resources.activeDepthTexture;
            if (!depth.IsValid() || !resources.activeColorTexture.IsValid()) return;

            // Match the camera depth attachment so opaque scenery occludes the outline mask.
            TextureDesc maskDesc = depth.GetDescriptor(renderGraph);
            maskDesc.name = "Interaction Hints Mask";
            maskDesc.format = GraphicsFormat.R8G8B8A8_UNorm;
            maskDesc.bindTextureMS = false;
            maskDesc.memoryless = RenderTextureMemoryless.None;
            maskDesc.isShadowMap = false;
            maskDesc.clearBuffer = true;
            maskDesc.clearColor = Color.clear;
            var mask = renderGraph.CreateTexture(maskDesc);

            using (var builder = renderGraph.AddRasterRenderPass<MaskData>("Interaction Hints Mask", out var data))
            {
                data.Draws = hints.Draws;
                data.Material = material;
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(depth, AccessFlags.Read);
                builder.SetRenderFunc(static (MaskData passData, RasterGraphContext context) =>
                {
                    foreach (var draw in passData.Draws)
                    {
                        for (int i = 0; i < draw.SubmeshCount; i++)
                            context.cmd.DrawRenderer(draw.Renderer, passData.Material, i, 0);
                    }
                });
            }

            using (var builder = renderGraph.AddRasterRenderPass<CompositeData>("Interaction Hints Composite", out var data))
            {
                data.Mask = mask;
                data.Material = material;
                data.OutlineColor = hints.OutlineColor;
                data.OutlineWidth = hints.OutlineWidth;
                builder.UseTexture(mask, AccessFlags.Read);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (CompositeData passData, RasterGraphContext context) =>
                {
                    Properties.Clear();
                    RTHandle maskTexture = passData.Mask;
                    float width = maskTexture.rt.width;
                    float height = maskTexture.rt.height;
                    Properties.SetTexture(BlitTexture, maskTexture);
                    Properties.SetVector(BlitTextureTexelSize, new Vector4(1f / width, 1f / height, width, height));
                    Properties.SetVector(BlitScaleBias, new Vector4(1, 1, 0, 0));
                    Properties.SetColor(OutlineColor, passData.OutlineColor);
                    Properties.SetInt(OutlineWidth, passData.OutlineWidth);
                    context.cmd.DrawProcedural(Matrix4x4.identity, passData.Material, 1, MeshTopology.Triangles, 3, 1, Properties);
                });
            }
        }

        private sealed class MaskData
        {
            internal List<InteractionHints.Draw> Draws;
            internal Material Material;
        }

        private sealed class CompositeData
        {
            internal TextureHandle Mask;
            internal Material Material;
            internal Color OutlineColor;
            internal int OutlineWidth;
        }
    }
}
