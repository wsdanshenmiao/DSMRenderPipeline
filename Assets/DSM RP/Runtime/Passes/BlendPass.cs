using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM
{
    public readonly ref struct BlendSetting
    {
        public readonly TextureHandle 
            m_SrcTexture, m_DstTexture;
        public readonly BlendMode m_SrcBlend;
        public readonly BlendMode m_DstBlend;
        public readonly BlendOp m_BlendOp;
        public BlendSetting(
            TextureHandle srcTex,
            TextureHandle dstTex,
            BlendMode srcBlend = BlendMode.SrcAlpha,
            BlendMode dstBlend = BlendMode.OneMinusSrcAlpha,
            BlendOp blendOp = BlendOp.Add)
        {
            m_SrcBlend = srcBlend;
            m_DstBlend = dstBlend;
            m_BlendOp = blendOp;
            m_SrcTexture = srcTex;
            m_DstTexture = dstTex;
        }
    }

    public class BlendPass
    {
        private static readonly ProfilingSampler sm_Sampler = new ProfilingSampler("Blend");

        private BlendMode m_SrcBlend;
        private BlendMode m_DstBlend;
        private BlendOp m_BlendOp;

        private TextureHandle m_SrcTexture;
        private TextureHandle m_DstTexture;

        static private Material sm_Material;

        private static readonly string sm_BlendShaderName = "DSM RP/Blend";

        public static readonly int
            m_SrcBlendId = Shader.PropertyToID("_SrcBlend"),
            m_DstBlendId = Shader.PropertyToID("_DstBlend"),
            m_BlendOpId = Shader.PropertyToID("_BlendOp"),
            m_SrcTextureId = Shader.PropertyToID("_SrcTexture");

        private void Render(RenderGraphContext context)
        {
            sm_Material = sm_Material == null ? 
                CoreUtils.CreateEngineMaterial(sm_BlendShaderName) : sm_Material;

            CommandBuffer cmd = context.cmd;
            sm_Material.SetFloat(m_SrcBlendId, (float)m_SrcBlend);
            sm_Material.SetFloat(m_DstBlendId, (float)m_DstBlend);
            sm_Material.SetFloat(m_BlendOpId, (float)m_BlendOp);
            sm_Material.SetTexture(m_SrcTextureId, m_SrcTexture);

            cmd.SetRenderTarget(m_DstTexture);

            cmd.DrawProcedural(Matrix4x4.identity, sm_Material, 0, MeshTopology.Triangles, 3);
        }

        public static void Record(RenderGraph renderGraph, BlendSetting setting)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sm_Sampler.name, out BlendPass pass, sm_Sampler);

            pass.m_SrcBlend = setting.m_SrcBlend;
            pass.m_DstBlend = setting.m_DstBlend;
            pass.m_BlendOp = setting.m_BlendOp;
            pass.m_SrcTexture = builder.ReadTexture(setting.m_SrcTexture);
            pass.m_DstTexture = builder.WriteTexture(setting.m_DstTexture);

            builder.SetRenderFunc<BlendPass>(
                static (pass, context) => pass.Render(context));
        }
    }
}