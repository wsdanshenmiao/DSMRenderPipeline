using System;
using Mono.Cecil;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Serialization;

namespace DSM
{
    [CreateAssetMenu(menuName = "Rendering/Custom PostEffect/SSR")]
    public class SSR : PostEffectManager.PostEffect
    {
        public enum SSRMode
        {
            ViewSpace = 0,
            ScreenSpace = 1,
            ScreenSpaceHieZ = 2
        }

        private const string
            m_SSRShaderName = "DSM RP/SSR",
            m_GenerateHieZShaderName = "GenerateSSRHieZ";

        private static readonly string[] m_SSRModeKeywords = {
            "SCREENSPACE", "SCREENSPACEHIEZ"
        };
        
        private static readonly int
            m_BackTextureId = Shader.PropertyToID("_BackTexture"),
            m_BlendSSRTextureId = Shader.PropertyToID("_BlendSSRTexture"),
            m_RayMarchingMaxCountId = Shader.PropertyToID("_RayMarchingMaxDistance"),
            m_RayMarchingStepId = Shader.PropertyToID("_RayMarchingStep"),
            m_HitThresholdId = Shader.PropertyToID("_HitThreshold"),
            m_HieZTextureId = Shader.PropertyToID("_HieZTexture"),
            m_DepthTextureId = Shader.PropertyToID("_DepthTexture"),
            m_StencilRefId = Shader.PropertyToID("_StencilRef");
        
        
        
        private Material m_Material;
        [SerializeField] private ComputeShader m_GenerateHieZComputeShader;
        
        [SerializeField] private int m_RayMarchingMaxDistance = 100;   // 最大步进次数
        [SerializeField] private float m_RayMarchingStep = 0.1f;    // 每次步进的步频
        [SerializeField] private float m_HitThreshold = 0.4f;
        [SerializeField] private SSRMode m_SSRMode = SSRMode.ScreenSpaceHieZ;
        [SerializeField] private uint m_HieZCount = 4;

        private RenderTexture[] m_HieZTextures = null;
        private RenderTexture m_PackHieZTexture = null;
        
        public override void Render(
            CommandBuffer cmd, 
            RenderTargetIdentifier src, 
            RenderTargetIdentifier dest,
            Camera camera)
        {
            if (m_GenerateHieZComputeShader == null) {
                Debug.LogError("GenerateHieZComputeShader is missing");
                return;
            }
            
            if (m_Material == null) {
                m_Material = CoreUtils.CreateEngineMaterial(Shader.Find(m_SSRShaderName));
            }
            
            if (m_SSRMode == SSRMode.ScreenSpaceHieZ) {
                int width = camera.pixelWidth, height = camera.pixelHeight;
                m_HieZTextures = new RenderTexture[m_HieZCount];
                m_PackHieZTexture = RenderTexture.GetTemporary(
                    width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
                m_PackHieZTexture.useMipMap = true;
                m_PackHieZTexture.autoGenerateMips = false;
                m_PackHieZTexture.Create();
                
                RenderTargetIdentifier depthTex = CameraRender.m_CameraDepthTextureId;
                cmd.Blit(depthTex, m_PackHieZTexture);
                // 需要手动生成，否则会被覆盖
                m_PackHieZTexture.GenerateMips();
                
                width = width / 2;
                height = height / 2;
                
                // 生成并设置层次深度
                for (int i = 0; i < m_HieZCount; ++i, width /= 2, height /= 2) {
                    m_HieZTextures[i] = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RFloat);
                    m_HieZTextures[i].enableRandomWrite = true;
                    m_HieZTextures[i].Create();
                    
                    cmd.SetComputeTextureParam(m_GenerateHieZComputeShader, 0, m_HieZTextureId, m_HieZTextures[i]);
                    cmd.SetComputeTextureParam(m_GenerateHieZComputeShader, 0, m_DepthTextureId, depthTex);
                    
                    cmd.DispatchCompute(m_GenerateHieZComputeShader, 0, m_HieZTextures[i].width, m_HieZTextures[i].height, 1);

                    depthTex = m_HieZTextures[i]; 
                    cmd.CopyTexture(depthTex, 0, 0, m_PackHieZTexture, 0, i + 1);
                }
                
                m_Material.SetTexture(m_HieZTextureId, m_PackHieZTexture);
            }
            
            SetKeywords(cmd, m_SSRModeKeywords, (int)m_SSRMode - 1);
            
            m_Material.SetInt(m_RayMarchingMaxCountId, m_RayMarchingMaxDistance);
            m_Material.SetFloat(m_RayMarchingStepId, m_RayMarchingStep);
            m_Material.SetFloat(m_HitThresholdId, m_HitThreshold);
            cmd.SetGlobalTexture(CameraRender.m_CameraColorTextureId, src);
            cmd.SetRenderTarget(dest, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                //CameraRender.m_CameraDepthTextureId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);


            if (m_SSRMode == SSRMode.ScreenSpaceHieZ) {
                for (int i = 0; i < m_HieZTextures.Length; ++i) {
                    RenderTexture.ReleaseTemporary(m_HieZTextures[i]);
                }
            }
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