using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM
{
    [CreateAssetMenu(menuName = "Rendering/Custom PostEffect/SSR")]
    public class SSRPassSetting : PostEffectSetting
    {
        private static ProfilingSampler sm_Sampler = new ProfilingSampler("SSR");

        public enum SSRMode
        {
            ViewSpace = 0,
            ScreenSpace = 1,
            ScreenSpaceHiz = 2
        }

        [Header("Marching Settings")]
        public int m_RayMarchingMaxDistance = 40;   // 最大步进次数
        [Range(0, float.MaxValue)] public float m_RayMarchingStep = 0.1f;    // 每次步进的步频
        [Range(0, 1)] public float m_HitThreshold = 0.4f;
        [Header("Hiz Settings")]
        public uint m_HizCount = 4;
        public uint m_HizStartLevel = 0;
        public uint m_HizEndLevel = 0;
        public ComputeShader m_GenerateHizShader = null;
        [Header("SSR Settings")]
        [Range(0, 1)] public float m_BlendFactor = 1;
        public SSRMode m_SSRMode = SSRMode.ScreenSpaceHiz;
        public BlendMode m_SrcBlend = BlendMode.SrcAlpha;
        public BlendMode m_SSRBlend = BlendMode.One;
        public BlendOp m_BlendOp = BlendOp.Add;
        [Header("Blur Settings")]
        [Range(0, 50)] public uint m_BlurRadius = 5;
        public ComputeShader m_BlurShader = null;
        [Header("")]
        public RenderingLayerMask m_RenderingLayerMask = 
            RenderingLayerMask.defaultRenderingLayerMask;

        public override void Record(
             RenderGraph renderGraph,
             CullingResults cullingResults,
             Camera camera,
             in CameraRendererTextures cameraTextures,
             TextureHandle target)
        {
            using RenderGraphBuilder ssrBuilder = renderGraph.AddRenderPass(
                sm_Sampler.name, out SSRPass pass, sm_Sampler);

            pass.m_Setting = this;
            pass.m_CameraWidth = camera.pixelWidth;
            pass.m_CameraHeight = camera.pixelHeight;

            // 使用颜色及深度图
            pass.m_SrcTexture = ssrBuilder.ReadWriteTexture(target);
            pass.m_DepthTexture = ssrBuilder.ReadTexture(cameraTextures.m_DepthTexture);
            pass.m_NormalTexture = ssrBuilder.ReadTexture(cameraTextures.m_NormalTexture);

            // 临时纹理
            int width = camera.pixelWidth, height = camera.pixelHeight;
            TextureDesc texDesc = new TextureDesc(width, height)
            {
                name = "MaskTexture",
                format = GraphicsFormat.R8_SNorm,
            };

            // 遮罩纹理
            pass.m_MaskTexture = ssrBuilder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));

            texDesc.format = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR);
            texDesc.name = "TmpTexture";
            pass.m_DstTexture = ssrBuilder.WriteTexture(renderGraph.CreateTexture(texDesc));

            // 打包好的Hiz纹理
            texDesc.format = GraphicsFormat.R32_SFloat;
            texDesc.name = "Package Hiz Texture";
            texDesc.useMipMap = true;
            texDesc.autoGenerateMips = false;   // 不能自动生成MipMap，否则拷贝的会被覆盖
            pass.m_PackageHizTexture = ssrBuilder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));

            // Hiz纹理
            pass.m_HizTextures = new TextureHandle[m_HizCount];
            texDesc.useMipMap = false;
            for (int i = 0, hizWidth = width / 2, hizHeight = height / 2;
                i < pass.m_HizTextures.Length; i++, hizWidth /= 2, hizHeight /= 2)
            {
                texDesc.width = hizWidth;
                texDesc.height = hizHeight;
                texDesc.enableRandomWrite = true;
                texDesc.name = "HizTexture" + i;
                pass.m_HizTextures[i] = ssrBuilder.ReadWriteTexture(
                    renderGraph.CreateTexture(texDesc));
            }

            pass.m_RenderList = ssrBuilder.UseRendererList(renderGraph.CreateRendererList(
                new RendererListDesc(SSRPass.m_ShaderTagID, cullingResults, camera)
                {
                    renderQueueRange = RenderQueueRange.all,
                    renderingLayerMask = m_RenderingLayerMask,
                    overrideMaterial = SSRPass.m_SSRLitMaterial,
                    overrideMaterialPassIndex = 1
                }));

            ssrBuilder.SetRenderFunc<SSRPass>(
                static (pass, context) => pass.Render(context));


            if (m_BlurShader != null)
            {
                GaussianBlurPass.Record(
                    renderGraph,
                    m_BlurShader,
                    pass.m_DstTexture,
                    m_BlurRadius,
                    width, height);
            }

            BlendSetting blendSetting = new BlendSetting(
                pass.m_DstTexture, pass.m_SrcTexture,
                m_SSRBlend, m_SrcBlend, m_BlendOp);
            BlendPass.Record(renderGraph, blendSetting);
        }
    }
}