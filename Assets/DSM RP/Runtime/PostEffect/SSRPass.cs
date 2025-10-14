using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.RendererUtils;

namespace DSM
{
    public class SSRPass
    {
        private static ProfilingSampler sm_Sampler = new ProfilingSampler("SSR");

        private Material m_Material;

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

        private int m_CameraWidth, m_CameraHeight;

        private SSRPassSetting m_Setting = null;

        public static readonly int
            m_RayMarchingMaxCountId = Shader.PropertyToID("_RayMarchingMaxDistance"),
            m_RayMarchingStepId = Shader.PropertyToID("_RayMarchingStep"),
            m_HitThresholdId = Shader.PropertyToID("_HitThreshold"),
            m_BlendFactorId = Shader.PropertyToID("_BlendFactor"),
            m_HizTextureId = Shader.PropertyToID("_HizTexture"),
            m_DepthTextureId = Shader.PropertyToID("_DepthTexture"),
            m_HizStartLevelId = Shader.PropertyToID("_HizStartLevel"),
            m_HizEndLevelId = Shader.PropertyToID("_HizEndLevel"),
            m_HizCountId = Shader.PropertyToID("_HizCount"),
            m_MaskTextureId = Shader.PropertyToID("_MaskTexture"),
            m_HizStrideId = Shader.PropertyToID("_HizStride");

        public static readonly Material m_SSRLitMaterial = 
            CoreUtils.CreateEngineMaterial(Shader.Find("DSM RP/SSRLit"));

        public static readonly ShaderTagId m_ShaderTagID = new("DSMLit");

        public static readonly string[] m_SSRModeKeywords = {
            "SCREENSPACE", "SCREENSPACEHIEZ"
        };

        public void Render(RenderGraphContext context)
        {
            if(m_Setting == null) return;
            if (m_Setting.m_GenerateHizShader == null) {
                Debug.LogError("GenerateHizComputeShader is missing");
                return;
            }
            
            CommandBuffer cmd = context.cmd;
            
            if (m_Setting.m_SSRMode == SSRPassSetting.SSRMode.ScreenSpaceHiz) {
                int width = m_CameraWidth, height = m_CameraHeight;
                m_Setting.m_HizCount = (uint)Mathf.Min(m_Setting.m_HizCount, Mathf.Log(Mathf.Min(width, height), 2));

                RenderTargetIdentifier depthTex = m_DepthTexture;
                cmd.Blit(depthTex, m_PackageHizTexture);
                
                width = width / 2;
                height = height / 2;
                
                // 生成并设置层次深度
                for (int i = 0; i < m_Setting.m_HizCount; ++i, width /= 2, height /= 2) {
                    var computeShader = m_Setting.m_GenerateHizShader;
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
            
            SetKeywords(cmd, m_SSRModeKeywords, (int)m_Setting.m_SSRMode - 1);
            
            SSRMaterial.SetInt(m_HizCountId, (int)m_Setting.m_HizCount);
            SSRMaterial.SetInt(m_HizStartLevelId, (int)Mathf.Min(m_Setting.m_HizStartLevel, m_Setting.m_HizCount));
            SSRMaterial.SetInt(m_HizEndLevelId, (int)Mathf.Min(m_Setting.m_HizEndLevel, m_Setting.m_HizStartLevel));
            SSRMaterial.SetInt(m_HizStrideId, (int)m_Setting.m_HizStride);
            SSRMaterial.SetInt(m_RayMarchingMaxCountId, m_Setting.m_RayMarchingMaxDistance);
            SSRMaterial.SetFloat(m_RayMarchingStepId, m_Setting.m_RayMarchingStep);
            SSRMaterial.SetFloat(m_HitThresholdId, m_Setting.m_HitThreshold);
            SSRMaterial.SetFloat(m_BlendFactorId, m_Setting.m_BlendFactor);

            SSRMaterial.SetTexture(CameraRendererTextures.m_CameraColorTextureId, m_SrcTexture);
            SSRMaterial.SetTexture(CameraRendererTextures.m_CameraDepthTextureId, m_DepthTexture);
            SSRMaterial.SetTexture(CameraRendererTextures.m_NormalTextureId, m_NormalTexture);
            SSRMaterial.SetTexture(m_MaskTextureId, m_MaskTexture);
            
            cmd.SetRenderTarget(m_DstTexture, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            
            cmd.DrawProcedural(Matrix4x4.identity, SSRMaterial, 0, MeshTopology.Triangles, 3);
            
            context.renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            CullingResults cullingResults,
            Camera camera,
            in CameraRendererTextures cameraTextures,
            TextureHandle target,
            SSRPassSetting setting)
        {
            using RenderGraphBuilder ssrBuilder = renderGraph.AddRenderPass(
                sm_Sampler.name, out SSRPass pass, sm_Sampler);

            int width = camera.pixelWidth, height = camera.pixelHeight;

            pass.m_Setting = setting;
            pass.m_CameraWidth = width;
            pass.m_CameraHeight = height;

            // 使用颜色及深度图
            pass.m_SrcTexture = ssrBuilder.ReadWriteTexture(target);
            pass.m_DepthTexture = ssrBuilder.ReadTexture(cameraTextures.m_DepthTexture);
            pass.m_NormalTexture = ssrBuilder.ReadTexture(cameraTextures.m_NormalTexture);

            // 遮罩纹理
            TextureDesc texDesc = new TextureDesc(width, height)
            {
                name = "MaskTexture",
                format = GraphicsFormat.R8_SNorm,
            };
            pass.m_MaskTexture = ssrBuilder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));

            // 临时纹理
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
            pass.m_HizTextures = new TextureHandle[setting.m_HizCount];
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
                    renderingLayerMask = setting.m_RenderingLayerMask,
                    overrideMaterial = SSRPass.m_SSRLitMaterial,
                    overrideMaterialPassIndex = 1
                }));

            ssrBuilder.SetRenderFunc<SSRPass>(
                static (pass, context) => pass.Render(context));


            if (setting.m_BlurShader != null)
            {
                GaussianBlurPass.Record(
                    renderGraph,
                    setting.m_BlurShader,
                    pass.m_DstTexture,
                    setting.m_BlurRadius,
                    width, height);
            }

            BlendSetting blendSetting = new BlendSetting(
                pass.m_DstTexture, pass.m_SrcTexture,
                setting.m_SSRBlend, setting.m_SrcBlend, setting.m_BlendOp);
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