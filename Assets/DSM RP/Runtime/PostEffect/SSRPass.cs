using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace DSM
{
    [CreateAssetMenu(menuName = "Rendering/Custom PostEffect/SSR")]
    public class SSRPass : PostEffectManager.PostEffect
    {
        public enum SSRMode
        {
            ViewSpace = 0,
            ScreenSpace = 1,
            ScreenSpaceHiz = 2
        }
        
        [SerializeField] private int m_RayMarchingMaxDistance = 100;   // 最大步进次数
        [SerializeField] private float m_RayMarchingStep = 0.1f;    // 每次步进的步频
        [SerializeField] private float m_HitThreshold = 0.4f;
        [SerializeField] private uint m_HizCount = 4;
        [SerializeField] private uint m_HizStartLevel = 0;
        [SerializeField] private uint m_HizEndLevel = 4;
        [SerializeField] private SSRMode m_SSRMode = SSRMode.ScreenSpaceHiz;
        [SerializeField] private RenderingLayerMask m_RenderingLayerMask = 
            RenderingLayerMask.defaultRenderingLayerMask;

        [SerializeField] private ComputeShader m_GenerateHizComputeShader;

        private static ProfilingSampler sm_Sampler = new ProfilingSampler("SSR");

        private TextureHandle m_ColorTexture, m_TmpTexture, 
            m_DepthTexture, m_NormalTexture;
        private TextureHandle[] m_HizTextures;
        private TextureHandle m_PackageHizTexture;

        private RendererListHandle m_RenderList;

        private Camera m_Camera;
        
        private static readonly int
            m_RayMarchingMaxCountId = Shader.PropertyToID("_RayMarchingMaxDistance"),
            m_RayMarchingStepId = Shader.PropertyToID("_RayMarchingStep"),
            m_HitThresholdId = Shader.PropertyToID("_HitThreshold"),
            m_HizTextureId = Shader.PropertyToID("_HizTexture"),
            m_DepthTextureId = Shader.PropertyToID("_DepthTexture"),
            m_HizStartLevelId = Shader.PropertyToID("_HizStartLevel"),
            m_HizEndLevelId = Shader.PropertyToID("_HizEndLevel"),
            m_HizCountId = Shader.PropertyToID("_HizCount");

        private const string m_SSRShaderName = "DSM RP/SSR";

        private static readonly string m_ShaderTagID;

        private static readonly string[] m_SSRModeKeywords = {
            "SCREENSPACE", "SCREENSPACEHIEZ"
        };

        private void Setup(
            int maxDistance,
            float rayMarchingStep,
            float hitThreshold,
            uint hizCount,
            uint hizStartLevel,
            uint hizEndLevel,
            SSRMode ssrMode,
            RenderingLayerMask renderingLayerMask,
            ComputeShader hizShader)
        {
            m_RayMarchingMaxDistance = maxDistance;
            m_RayMarchingStep = rayMarchingStep;
            m_HitThreshold = hitThreshold;
            m_HizCount = hizCount;
            m_HizStartLevel = hizStartLevel;
            m_HizEndLevel = hizEndLevel;
            m_SSRMode = ssrMode;
            m_RenderingLayerMask = renderingLayerMask;
            m_GenerateHizComputeShader = hizShader;
        }

        protected override void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;

            if (m_GenerateHizComputeShader == null) {
                Debug.LogError("GenerateHizComputeShader is missing");
                return;
            }
            
            if (m_Material == null) {
                m_Material = CoreUtils.CreateEngineMaterial(Shader.Find(m_SSRShaderName));
            }
            
            if (m_SSRMode == SSRMode.ScreenSpaceHiz) {
                int width = m_Camera.pixelWidth, height = m_Camera.pixelHeight;
                m_HizCount = (uint)Mathf.Min(m_HizCount, Mathf.Log(Mathf.Min(width, height), 2));

                RenderTargetIdentifier depthTex = m_DepthTexture;
                cmd.Blit(depthTex, m_PackageHizTexture);
                
                width = width / 2;
                height = height / 2;
                
                // 生成并设置层次深度
                for (int i = 0; i < m_HizCount; ++i, width /= 2, height /= 2) {
                    cmd.SetComputeTextureParam(m_GenerateHizComputeShader, 0, m_HizTextureId, m_HizTextures[i]);
                    cmd.SetComputeTextureParam(m_GenerateHizComputeShader, 0, m_DepthTextureId, depthTex);
                    
                    cmd.DispatchCompute(m_GenerateHizComputeShader, 0, width, height, 1);

                    depthTex = m_HizTextures[i];
                    cmd.CopyTexture(depthTex, 0, 0, m_PackageHizTexture, 0, i + 1);
                }
                
                m_Material.SetTexture(m_HizTextureId, m_PackageHizTexture);
            }
            
            SetKeywords(cmd, m_SSRModeKeywords, (int)m_SSRMode - 1);
            
            m_Material.SetInt(m_HizCountId, (int)m_HizCount);
            m_Material.SetInt(m_HizStartLevelId, (int)Mathf.Min(m_HizStartLevel, m_HizCount));
            m_Material.SetInt(m_HizEndLevelId, (int)Mathf.Min(m_HizEndLevel, m_HizStartLevel));
            m_Material.SetInt(m_RayMarchingMaxCountId, m_RayMarchingMaxDistance);
            m_Material.SetFloat(m_RayMarchingStepId, m_RayMarchingStep);
            m_Material.SetFloat(m_HitThresholdId, m_HitThreshold);
            cmd.SetGlobalTexture(CameraRendererTextures.m_CameraColorTextureId, m_ColorTexture);
            cmd.SetGlobalTexture(CameraRendererTextures.m_CameraDepthTextureId, m_DepthTexture);
            cmd.SetGlobalTexture(CameraRendererTextures.m_NormalTextureId, m_NormalTexture);
            cmd.SetRenderTarget(m_TmpTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                //CameraRender.m_CameraDepthTextureId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);

            cmd.Blit(m_TmpTexture, m_ColorTexture);
        }

        public override void Record(
            RenderGraph renderGraph, 
            CullingResults cullingResults, 
            Camera camera, 
            ScriptableRenderContext renderContext, 
            in CameraRendererTextures cameraTextures)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sm_Sampler.name, out SSRPass pass, sm_Sampler);

            pass.Setup(
                m_RayMarchingMaxDistance, m_RayMarchingStep,
                m_HitThreshold, m_HizCount,
                m_HizStartLevel, m_HizEndLevel,
                m_SSRMode, m_RenderingLayerMask,
                m_GenerateHizComputeShader);
            pass.m_Camera = camera;
            
            // 使用颜色及深度图
            pass.m_ColorTexture = builder.ReadTexture(cameraTextures.m_ColorTexture);
            pass.m_DepthTexture = builder.ReadTexture(cameraTextures.m_DepthTexture);
            pass.m_NormalTexture = builder.ReadTexture(cameraTextures.m_NormalTexture);

            // 临时纹理
            int width = camera.pixelWidth, height = camera.pixelHeight;
            TextureDesc texDesc = new TextureDesc(width, height)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR),
                name = "SSRTmp Texture"
            };
            pass.m_TmpTexture = builder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));

            // 打包好的Hiz纹理
            texDesc.format = GraphicsFormat.R32_SFloat;
            texDesc.name = "Package Hiz Texture";
            texDesc.useMipMap = true;
            texDesc.autoGenerateMips = false;   // 不能自动生成MipMap，否则拷贝的会被覆盖
            pass.m_PackageHizTexture = builder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));

            // Hiz纹理
            pass.m_HizTextures = new TextureHandle[pass.m_HizCount];
            texDesc.useMipMap = false;
            for (int i = 0, hizWidth = width / 2, hizHeight = height / 2; 
                i < pass.m_HizTextures.Length; i++, hizWidth /= 2, hizHeight /= 2) {
                texDesc.width = hizWidth;
                texDesc.height = hizHeight;
                texDesc.enableRandomWrite = true;
                texDesc.name = "HizTexture" + i;
                pass.m_HizTextures[i] = builder.ReadWriteTexture(
                    renderGraph.CreateTexture(texDesc));
            }

            RendererListDesc rendererListDesc = new RendererListDesc(
                new ShaderTagId(SSRPass.m_ShaderTagID), cullingResults, camera);
            //pass.m_RenderList = builder.UseRendererList(renderGraph.CreateRendererList(rendererListDesc));

            builder.SetRenderFunc<SSRPass>(
                static (pass, context) => pass.Render(context));
        }


        private void SetKeywords(CommandBuffer cmd, string[] keywords, int enableIndex)
        {
            for (int i = 0; i < keywords.Length; ++i) {
                if (i == enableIndex) {
                    cmd.EnableShaderKeyword(keywords[i]);
                }
                else {
                    cmd.DisableShaderKeyword(keywords[i]);
                }
            }
        }
    }
}