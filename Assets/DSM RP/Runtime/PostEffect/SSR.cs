using UnityEngine.Rendering;
using UnityEngine;

namespace DSM
{
    //[CreateAssetMenu(menuName = "Rendering/Custom PostEffect/SSR")]
    public class SSR
    {
        private const string m_ShaderName = "DSM RP/SSR";
        
        static readonly private int 
            m_NormalTextureId = Shader.PropertyToID("_SSRNormalTexture");
        
        private Material m_Material;
        
        public void Render(
            CommandBuffer cmd, 
            RenderTargetIdentifier src, 
            RenderTargetIdentifier dest,
            RenderTexture normalTexture, 
            Camera camera)
        {
            if (m_Material == null) {
                m_Material = CoreUtils.CreateEngineMaterial(Shader.Find(m_ShaderName));
            }

            cmd.SetGlobalTexture(m_NormalTextureId, normalTexture);
            cmd.SetGlobalTexture(CameraRender.m_CameraColorTextureId, src);
            cmd.SetRenderTarget(dest, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);
        }
    }
}