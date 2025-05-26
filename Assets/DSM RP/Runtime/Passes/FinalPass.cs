using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;

namespace DSM {
    public class FinalPass
    {
        private static readonly ProfilingSampler sm_Sampler = new("Final");

        TextureHandle m_ColorTexture;

        void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;
            cmd.Blit(m_ColorTexture, BuiltinRenderTextureType.CameraTarget);
            context.renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            in CameraRendererTextures textures)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sm_Sampler.name, out FinalPass pass, sm_Sampler);
            pass.m_ColorTexture = builder.ReadTexture(textures.m_ColorTexture);
            builder.SetRenderFunc<FinalPass>(
                static (pass, context) => pass.Render(context));
        }
    }
}