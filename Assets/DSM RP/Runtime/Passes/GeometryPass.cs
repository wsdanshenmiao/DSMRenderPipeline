using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM
{
    public class GeometryPass
    {
        private static readonly ProfilingSampler
            sm_OpaqueSampler = new ProfilingSampler("Opaque Geometry"),
            sm_TransparentSampler = new ProfilingSampler("Transparent Geometry");

        private static readonly ShaderTagId[] m_ShaderTagIDs = {
            new("SRPDefaultUnlit"),
            new("DSMLit")
        };

        private RendererListHandle m_RenderList;

        private void Render(RenderGraphContext context)
        {
            context.cmd.DrawRendererList(m_RenderList);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            CullingResults cullingResults,
            Camera camera,
            uint renderingLayerMask,
            bool opaque,
            in CameraRendererTextures cameraTextures,
            in LightResources lightResources)
        {
            var sampler = opaque ? sm_OpaqueSampler : sm_TransparentSampler;

            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sampler.name, out GeometryPass pass, sampler);

            pass.m_RenderList = builder.UseRendererList(renderGraph.CreateRendererList(
                new RendererListDesc(m_ShaderTagIDs, cullingResults, camera) {
                    sortingCriteria = opaque ?
                        SortingCriteria.CommonOpaque :
                        SortingCriteria.CommonTransparent,
                    rendererConfiguration =
                        PerObjectData.ReflectionProbes |
                        PerObjectData.Lightmaps |
                        PerObjectData.ShadowMask |
                        PerObjectData.LightProbe |
                        PerObjectData.OcclusionProbe |
                        PerObjectData.LightProbeProxyVolume |
                        PerObjectData.OcclusionProbeProxyVolume,
                    renderQueueRange = opaque ?
                        RenderQueueRange.opaque : RenderQueueRange.transparent,
                    renderingLayerMask = renderingLayerMask
                }));

            builder.ReadWriteTexture(cameraTextures.m_ColorTexture);
            builder.ReadWriteTexture(cameraTextures.m_DepthTexture);
            if (!opaque) {
                if (cameraTextures.m_ColorCopy.IsValid()) {
                    builder.ReadTexture(cameraTextures.m_ColorCopy);
                }
                if (cameraTextures.m_DepthCopy.IsValid()) {
                    builder.ReadTexture(cameraTextures.m_DepthCopy);
                }
            }

            builder.ReadTexture(lightResources.m_ShadowResources.m_DirectionalShadowMap);

            builder.SetRenderFunc<GeometryPass>(
                static (pass, context) => { pass.Render(context); });
        }
    }
}