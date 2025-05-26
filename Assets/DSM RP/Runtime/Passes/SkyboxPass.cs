using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine;

namespace DSM{

    public class SkyboxPass
    {
        static readonly ProfilingSampler sm_Sampler = new("Skybox");

        RendererListHandle m_RenderList;

        void Render(RenderGraphContext context)
        {
            context.cmd.DrawRendererList(m_RenderList);
            context.renderContext.ExecuteCommandBuffer(context.cmd);
            context.cmd.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            Camera camera,
            in CameraRendererTextures textures)
        {
            if (camera.clearFlags == CameraClearFlags.Skybox)
            {
                using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                    sm_Sampler.name, out SkyboxPass pass, sm_Sampler);
                pass.m_RenderList = builder.UseRendererList(
                    renderGraph.CreateSkyboxRendererList(camera));
                builder.AllowPassCulling(false);
                builder.ReadWriteTexture(textures.m_ColorTexture);
                builder.ReadTexture(textures.m_DepthTexture);
                builder.SetRenderFunc<SkyboxPass>(
                    static (pass, context) => pass.Render(context));
            }
        }
    }

}