using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RendererUtils;
using static UnityEditor.ObjectChangeEventStream;

namespace DSM
{
    [CreateAssetMenu(menuName = "Rendering/Custom PostEffect/SSR")]
    public class SSRPassSetting : PostEffectManager.PostEffectSetting
    {
        private static SSRPass m_Pass = null;

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
        [Header("SSR Settings")]
        [Range(0, 1)] public float m_BlendFactor = 1;
        public SSRMode m_SSRMode = SSRMode.ScreenSpaceHiz;
        public BlendMode m_SrcBlend = BlendMode.SrcAlpha;
        public BlendMode m_DstBlend = BlendMode.One;
        public BlendOp m_BlendOp = BlendOp.Add;
        [Header("")]
        public RenderingLayerMask m_RenderingLayerMask = 
            RenderingLayerMask.defaultRenderingLayerMask;
        public ComputeShader m_GenerateHizComputeShader;

        public override PostEffectManager.PostEffect CreatePostEffect()
        {
            if(m_Pass == null) { m_Pass = new SSRPass(this); }
            else { m_Pass.Setup(this); }
            return m_Pass;
        }
    }
    
    public class SSRPass : PostEffectManager.PostEffect
    {
        private static ProfilingSampler sm_Sampler = new ProfilingSampler("SSR");
        
        private SSRPassSetting m_Setting;

        private TextureHandle m_SrcTexture, m_DstTexture, 
            m_DepthTexture, m_NormalTexture;
        private TextureHandle[] m_HizTextures;
        private TextureHandle m_PackageHizTexture;
        private TextureHandle m_MaskTexture;

        private RendererListHandle m_RenderList;

        private Camera m_Camera;

        private static readonly int
            m_RayMarchingMaxCountId = Shader.PropertyToID("_RayMarchingMaxDistance"),
            m_RayMarchingStepId = Shader.PropertyToID("_RayMarchingStep"),
            m_HitThresholdId = Shader.PropertyToID("_HitThreshold"),
            m_BlendFactorId = Shader.PropertyToID("_BlendFactor"),
            m_HizTextureId = Shader.PropertyToID("_HizTexture"),
            m_DepthTextureId = Shader.PropertyToID("_DepthTexture"),
            m_HizStartLevelId = Shader.PropertyToID("_HizStartLevel"),
            m_HizEndLevelId = Shader.PropertyToID("_HizEndLevel"),
            m_HizCountId = Shader.PropertyToID("_HizCount");

        private static readonly string 
            m_SSRShaderName = "DSM RP/SSR",
            m_SSRLitShaderName = "DSM RP/SSRLit";

        private static readonly ShaderTagId m_ShaderTagID = new("DSMLit");

        private static readonly string[] m_SSRModeKeywords = {
            "SCREENSPACE", "SCREENSPACEHIEZ"
        };

        public SSRPass() { }

        public SSRPass(SSRPassSetting setting)
        {
            m_Setting = setting;
        }

        public void Setup(SSRPassSetting setting)
        {
            m_Setting = setting;
        }

        protected override void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;

            if (m_Setting.m_GenerateHizComputeShader == null) {
                Debug.LogError("GenerateHizComputeShader is missing");
                return;
            }
            
            m_Material = m_Material == null ? 
                CoreUtils.CreateEngineMaterial(Shader.Find(m_SSRShaderName)) : m_Material;
            
            if (m_Setting.m_SSRMode == SSRPassSetting.SSRMode.ScreenSpaceHiz) {
                int width = m_Camera.pixelWidth, height = m_Camera.pixelHeight;
                m_Setting.m_HizCount = (uint)Mathf.Min(m_Setting.m_HizCount, Mathf.Log(Mathf.Min(width, height), 2));

                RenderTargetIdentifier depthTex = m_DepthTexture;
                cmd.Blit(depthTex, m_PackageHizTexture);
                
                width = width / 2;
                height = height / 2;
                
                // 生成并设置层次深度
                for (int i = 0; i < m_Setting.m_HizCount; ++i, width /= 2, height /= 2) {
                    var computeShader = m_Setting.m_GenerateHizComputeShader;
                    cmd.SetComputeTextureParam(computeShader, 0, m_HizTextureId, m_HizTextures[i]);
                    cmd.SetComputeTextureParam(computeShader, 0, m_DepthTextureId, depthTex);
                    
                    cmd.DispatchCompute(computeShader, 0, width, height, 1);

                    depthTex = m_HizTextures[i];
                    cmd.CopyTexture(depthTex, 0, 0, m_PackageHizTexture, 0, i + 1);
                }
                
                m_Material.SetTexture(m_HizTextureId, m_PackageHizTexture);
            }
            
            //cmd.SetRenderTarget(m_MaskTexture);
            //cmd.DrawRendererList(m_RenderList);
            
            SetKeywords(cmd, m_SSRModeKeywords, (int)m_Setting.m_SSRMode - 1);
            
            m_Material.SetInt(m_HizCountId, (int)m_Setting.m_HizCount);
            m_Material.SetInt(m_HizStartLevelId, (int)Mathf.Min(m_Setting.m_HizStartLevel, m_Setting.m_HizCount));
            m_Material.SetInt(m_HizEndLevelId, (int)Mathf.Min(m_Setting.m_HizEndLevel, m_Setting.m_HizStartLevel));
            m_Material.SetInt(m_RayMarchingMaxCountId, m_Setting.m_RayMarchingMaxDistance);
            m_Material.SetFloat(m_RayMarchingStepId, m_Setting.m_RayMarchingStep);
            m_Material.SetFloat(m_HitThresholdId, m_Setting.m_HitThreshold);
            m_Material.SetFloat(m_BlendFactorId, m_Setting.m_BlendFactor);

            cmd.SetGlobalTexture(CameraRendererTextures.m_CameraColorTextureId, m_SrcTexture);
            cmd.SetGlobalTexture(CameraRendererTextures.m_CameraDepthTextureId, m_DepthTexture);
            cmd.SetGlobalTexture(CameraRendererTextures.m_NormalTextureId, m_NormalTexture);
            cmd.SetRenderTarget(m_DstTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            
            cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);
        }

        public override void Record(
            RenderGraph renderGraph, 
            CullingResults cullingResults, 
            Camera camera, 
            ScriptableRenderContext renderContext, 
            in CameraRendererTextures cameraTextures)
        {
            using RenderGraphBuilder ssrBuilder = renderGraph.AddRenderPass(
                sm_Sampler.name, out SSRPass pass, sm_Sampler);

            pass.Setup(m_Setting);
            pass.m_Camera = camera;
            
            // 使用颜色及深度图
            pass.m_SrcTexture = ssrBuilder.ReadTexture(cameraTextures.m_ColorTexture);
            pass.m_DepthTexture = ssrBuilder.ReadTexture(cameraTextures.m_DepthTexture);
            pass.m_NormalTexture = ssrBuilder.ReadTexture(cameraTextures.m_NormalTexture);

            // 临时纹理
            int width = camera.pixelWidth, height = camera.pixelHeight;
            TextureDesc texDesc = new TextureDesc(width, height)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.HDR),
                name = "SSRTmp Texture"
            };
            pass.m_DstTexture = ssrBuilder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));
            
            // 遮罩纹理
            texDesc.name = "MaskTexture";
            texDesc.format = GraphicsFormat.R8_SNorm;
            pass.m_MaskTexture = ssrBuilder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));
            
            // 打包好的Hiz纹理
            texDesc.format = GraphicsFormat.R32_SFloat;
            texDesc.name = "Package Hiz Texture";
            texDesc.useMipMap = true;
            texDesc.autoGenerateMips = false;   // 不能自动生成MipMap，否则拷贝的会被覆盖
            pass.m_PackageHizTexture = ssrBuilder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));

            // Hiz纹理
            pass.m_HizTextures = new TextureHandle[m_Setting.m_HizCount];
            texDesc.useMipMap = false;
            for (int i = 0, hizWidth = width / 2, hizHeight = height / 2; 
                i < pass.m_HizTextures.Length; i++, hizWidth /= 2, hizHeight /= 2) {
                texDesc.width = hizWidth;
                texDesc.height = hizHeight;
                texDesc.enableRandomWrite = true;
                texDesc.name = "HizTexture" + i;
                pass.m_HizTextures[i] = ssrBuilder.ReadWriteTexture(
                    renderGraph.CreateTexture(texDesc));
            }

            pass.m_RenderList = m_RenderList = ssrBuilder.UseRendererList(renderGraph.CreateRendererList(
                new RendererListDesc(m_ShaderTagID, cullingResults, camera)
                {
                    renderQueueRange = RenderQueueRange.all,
                    renderingLayerMask = m_Setting.m_RenderingLayerMask,
                    overrideShader = Shader.Find(m_SSRLitShaderName),
                    overrideShaderPassIndex = 1
                }));

            ssrBuilder.SetRenderFunc<SSRPass>(
                static (pass, context) => pass.Render(context));
            
            BlendSetting blendSetting = new BlendSetting(
                pass.m_DstTexture, pass.m_SrcTexture,
                pass.m_Setting.m_SrcBlend, pass.m_Setting.m_DstBlend, pass.m_Setting.m_BlendOp);
            BlendPass.Record(renderGraph, blendSetting);
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