using UnityEngine.Rendering;
using UnityEngine;

namespace DSM
{
    [CreateAssetMenu(menuName = "Rendering/Custom PostEffect/SSR")]
    public class SSR : PostEffectManager.PostEffect
    {
        private const string m_ShaderName = "DSM RP/SSR";

        static readonly private int
            m_BackTextureId = Shader.PropertyToID("_BackTexture"),
            m_BlendSSRTextureId = Shader.PropertyToID("_BlendSSRTexture"),
            m_RayMarchingMaxCountId = Shader.PropertyToID("_RayMarchingMaxCount"),
            m_RayMarchingStepId = Shader.PropertyToID("_RayMarchingStep"),
            m_HitThresholdId = Shader.PropertyToID("_HitThreshold"),
            m_StencilRefId = Shader.PropertyToID("_StencilRef");
        
        private Material m_Material;
        
        [SerializeField] private float m_StencilRef = 200;
        [SerializeField] private int m_RayMarchingMaxCount = 400;   // 最大步进次数
        [SerializeField] private float m_RayMarchingStep = 0.1f;    // 每次步进的步频
        [SerializeField] private float m_HitThreshold = 0.4f;
        
        public override void Render(
            CommandBuffer cmd, 
            RenderTargetIdentifier src, 
            RenderTargetIdentifier dest)
        {
            if (m_Material == null) {
                m_Material = CoreUtils.CreateEngineMaterial(Shader.Find(m_ShaderName));
            }

            m_Material.SetFloat(m_StencilRefId, m_StencilRef);
            m_Material.SetInt(m_RayMarchingMaxCountId, m_RayMarchingMaxCount);
            m_Material.SetFloat(m_RayMarchingStepId, m_RayMarchingStep);
            m_Material.SetFloat(m_HitThresholdId, m_HitThreshold);
            
            cmd.SetGlobalTexture(CameraRender.m_CameraColorTextureId, src);
            cmd.SetRenderTarget(dest, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                //CameraRender.m_CameraDepthTextureId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);
        }
    }
}