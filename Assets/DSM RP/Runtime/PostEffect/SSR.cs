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
            ViewSpace, ScreenSpace, ScreenSpaceHieZ
        }
        
        public SSRMode m_SSRMode = SSRMode.ScreenSpaceHieZ;
        public uint m_HieZCount = 4;
        
        
        private const string 
            m_SSRShaderName = "DSM RP/SSR",
            m_GenerateHieZShaderName = "GenerateSSRHieZ";

        static readonly private int
            m_BackTextureId = Shader.PropertyToID("_BackTexture"),
            m_BlendSSRTextureId = Shader.PropertyToID("_BlendSSRTexture"),
            m_RayMarchingMaxCountId = Shader.PropertyToID("_RayMarchingMaxDistance"),
            m_RayMarchingStepId = Shader.PropertyToID("_RayMarchingStep"),
            m_HitThresholdId = Shader.PropertyToID("_HitThreshold"),
            m_HieZTextureId = Shader.PropertyToID("_HieZTexture"),
            m_DepthTextureId = Shader.PropertyToID("_DepthTexture"),
            m_StencilRefId = Shader.PropertyToID("_StencilRef");
        
        private Material m_Material;
        [FormerlySerializedAs("m_GenerateHieZShader")] [SerializeField] private ComputeShader m_GenerateHieZComputeShader;
        
        [SerializeField] private float m_StencilRef = 200;
        [SerializeField] private int m_RayMarchingMaxDistance = 100;   // 最大步进次数
        [SerializeField] private float m_RayMarchingStep = 0.1f;    // 每次步进的步频
        [SerializeField] private float m_HitThreshold = 0.4f;

        private RenderTexture[] m_HieZTexture = null;
        
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
                m_HieZTexture = new RenderTexture[m_HieZCount];
                RenderTargetIdentifier depthTex = CameraRender.m_CameraDepthTextureId;
                int width = Mathf.NextPowerOfTwo(camera.pixelWidth) / 2;
                int height = Mathf.NextPowerOfTwo(camera.pixelHeight) /2;
                // 生成并设置层次深度
                for (int i = 0; i < m_HieZCount; depthTex = m_HieZTexture[i], ++i, width /= 2, height /= 2) {
                    m_HieZTexture[i] = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RFloat);
                    m_HieZTexture[i].enableRandomWrite = true;
                    m_HieZTexture[i].Create();
                    
                    cmd.SetComputeTextureParam(m_GenerateHieZComputeShader, 0, m_HieZTextureId, m_HieZTexture[i]);
                    cmd.SetComputeTextureParam(m_GenerateHieZComputeShader, 0, m_DepthTextureId, depthTex);
                    
                    cmd.DispatchCompute(m_GenerateHieZComputeShader, 0, m_HieZTexture[i].width, m_HieZTexture[i].height, 1);
                }
            }
            
            m_Material.SetFloat(m_StencilRefId, m_StencilRef);
            m_Material.SetInt(m_RayMarchingMaxCountId, m_RayMarchingMaxDistance);
            m_Material.SetFloat(m_RayMarchingStepId, m_RayMarchingStep);
            m_Material.SetFloat(m_HitThresholdId, m_HitThreshold);
            cmd.SetGlobalTexture(CameraRender.m_CameraColorTextureId, src);
            cmd.SetRenderTarget(dest, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                //CameraRender.m_CameraDepthTextureId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);


            if (m_SSRMode == SSRMode.ScreenSpaceHieZ) {
                for (int i = 0; i < m_HieZTexture.Length; ++i) {
                    RenderTexture.ReleaseTemporary(m_HieZTexture[i]);
                }
            }
        }
    }
}