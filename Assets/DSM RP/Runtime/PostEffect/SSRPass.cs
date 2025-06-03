using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Serialization;

namespace DSM
{
    public class SSRPass : PostEffect
    {
        private static ProfilingSampler sm_Sampler = new ProfilingSampler("SSR");
        
        public static SSRPassSetting sm_Setting;
        
        public Material SSRMaterial{
            get{
                if (m_Material == null) {
                    m_Material = CoreUtils.CreateEngineMaterial(Shader.Find("DSM RP/SSR"));
                }
                return m_Material;
            }
            
        }

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
            m_HizCountId = Shader.PropertyToID("_HizCount"),
            m_MaskTextureId = Shader.PropertyToID("_MaskTexture");

        private static readonly Material m_SSRLitMaterial = 
            CoreUtils.CreateEngineMaterial(Shader.Find("DSM RP/SSRLit"));

        private static readonly ShaderTagId m_ShaderTagID = new("DSMLit");

        private static readonly string[] m_SSRModeKeywords = {
            "SCREENSPACE", "SCREENSPACEHIEZ"
        };

        protected override void Render(RenderGraphContext context)
        {
            if(sm_Setting == null) return;
            if (sm_Setting.m_GenerateHizShader == null) {
                Debug.LogError("GenerateHizComputeShader is missing");
                return;
            }
            
            CommandBuffer cmd = context.cmd;
            
            if (sm_Setting.m_SSRMode == SSRPassSetting.SSRMode.ScreenSpaceHiz) {
                int width = m_Camera.pixelWidth, height = m_Camera.pixelHeight;
                sm_Setting.m_HizCount = (uint)Mathf.Min(sm_Setting.m_HizCount, Mathf.Log(Mathf.Min(width, height), 2));

                RenderTargetIdentifier depthTex = m_DepthTexture;
                cmd.Blit(depthTex, m_PackageHizTexture);
                
                width = width / 2;
                height = height / 2;
                
                // 生成并设置层次深度
                for (int i = 0; i < sm_Setting.m_HizCount; ++i, width /= 2, height /= 2) {
                    var computeShader = sm_Setting.m_GenerateHizShader;
                    int kernelIndex = computeShader.FindKernel("GenerateSSRHieZ");
                    cmd.SetComputeTextureParam(computeShader, 0, m_HizTextureId, m_HizTextures[i]);
                    cmd.SetComputeTextureParam(computeShader, 0, m_DepthTextureId, depthTex);
                    
                    cmd.DispatchCompute(computeShader, 0, width, height, 1);

                    depthTex = m_HizTextures[i];
                    cmd.CopyTexture(depthTex, 0, 0, m_PackageHizTexture, 0, i + 1);
                }
                
                SSRMaterial.SetTexture(m_HizTextureId, m_PackageHizTexture);
            }
            
            cmd.SetRenderTarget(m_MaskTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                m_DepthTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.DrawRendererList(m_RenderList);
            
            SetKeywords(cmd, m_SSRModeKeywords, (int)sm_Setting.m_SSRMode - 1);
            
            SSRMaterial.SetInt(m_HizCountId, (int)sm_Setting.m_HizCount);
            SSRMaterial.SetInt(m_HizStartLevelId, (int)Mathf.Min(sm_Setting.m_HizStartLevel, sm_Setting.m_HizCount));
            SSRMaterial.SetInt(m_HizEndLevelId, (int)Mathf.Min(sm_Setting.m_HizEndLevel, sm_Setting.m_HizStartLevel));
            SSRMaterial.SetInt(m_RayMarchingMaxCountId, sm_Setting.m_RayMarchingMaxDistance);
            SSRMaterial.SetFloat(m_RayMarchingStepId, sm_Setting.m_RayMarchingStep);
            SSRMaterial.SetFloat(m_HitThresholdId, sm_Setting.m_HitThreshold);
            SSRMaterial.SetFloat(m_BlendFactorId, sm_Setting.m_BlendFactor);

            SSRMaterial.SetTexture(CameraRendererTextures.m_CameraColorTextureId, m_SrcTexture);
            SSRMaterial.SetTexture(CameraRendererTextures.m_CameraDepthTextureId, m_DepthTexture);
            SSRMaterial.SetTexture(CameraRendererTextures.m_NormalTextureId, m_NormalTexture);
            SSRMaterial.SetTexture(m_MaskTextureId, m_MaskTexture);
            
            cmd.SetRenderTarget(m_DstTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            
            cmd.DrawProcedural(Matrix4x4.identity, SSRMaterial, 0, MeshTopology.Triangles, 3);
            
            context.renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public override void Record(
            RenderGraph renderGraph, 
            CullingResults cullingResults, 
            Camera camera, 
            in CameraRendererTextures cameraTextures,
            TextureHandle target)
        {
            using RenderGraphBuilder ssrBuilder = renderGraph.AddRenderPass(
                sm_Sampler.name, out SSRPass pass, sm_Sampler);
            
            pass.m_Camera = camera;
            
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
            pass.m_HizTextures = new TextureHandle[sm_Setting.m_HizCount];
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

            pass.m_RenderList = ssrBuilder.UseRendererList(renderGraph.CreateRendererList(
                new RendererListDesc(m_ShaderTagID, cullingResults, camera)
                {
                    renderQueueRange = RenderQueueRange.all,
                    renderingLayerMask = sm_Setting.m_RenderingLayerMask,
                    overrideMaterial = m_SSRLitMaterial,
                    overrideMaterialPassIndex = 1
                }));
            
            ssrBuilder.SetRenderFunc<SSRPass>(
                static (pass, context) => pass.Render(context));


            if (sm_Setting.m_BlurShader != null) {
                GaussianBlurPass.Record(
                    renderGraph, 
                    sm_Setting.m_BlurShader, 
                    pass.m_DstTexture, 
                    sm_Setting.m_BlurRadius, 
                    width, height);
            }
            
            BlendSetting blendSetting = new BlendSetting(
                pass.m_DstTexture, pass.m_SrcTexture,
                sm_Setting.m_SSRBlend, sm_Setting.m_SrcBlend, sm_Setting.m_BlendOp);
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