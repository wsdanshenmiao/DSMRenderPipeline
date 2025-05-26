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
            ScreenSpaceHiz = 2
        }
        
        private Material m_Material;
        [SerializeField] private ComputeShader m_GenerateHizComputeShader;
        
        [SerializeField] private int m_RayMarchingMaxDistance = 100;   // 最大步进次数
        [SerializeField] private float m_RayMarchingStep = 0.1f;    // 每次步进的步频
        [SerializeField] private float m_HitThreshold = 0.4f;
        [SerializeField] private SSRMode m_SSRMode = SSRMode.ScreenSpaceHiz;
        [SerializeField] private uint m_HizCount = 4;
        [SerializeField] private uint m_HizStartLevel = 0;
        [SerializeField] private uint m_HizEndLevel = 4;

        private RenderTexture[] m_HizTextures = null;
        private RenderTexture m_PackHizTexture = null;
        
        private static readonly int
            m_BackTextureId = Shader.PropertyToID("_BackTexture"),
            m_BlendSSRTextureId = Shader.PropertyToID("_BlendSSRTexture"),
            m_RayMarchingMaxCountId = Shader.PropertyToID("_RayMarchingMaxDistance"),
            m_RayMarchingStepId = Shader.PropertyToID("_RayMarchingStep"),
            m_HitThresholdId = Shader.PropertyToID("_HitThreshold"),
            m_HizTextureId = Shader.PropertyToID("_HizTexture"),
            m_DepthTextureId = Shader.PropertyToID("_DepthTexture"),
            m_HizStartLevelId = Shader.PropertyToID("_HizStartLevel"),
            m_HizEndLevelId = Shader.PropertyToID("_HizEndLevel"),
            m_HizCountId = Shader.PropertyToID("_HizCount");

        private const string
            m_SSRShaderName = "DSM RP/SSR",
            m_GenerateHizShaderName = "GenerateSSRHiz";

        private static readonly string[] m_SSRModeKeywords = {
            "SCREENSPACE", "SCREENSPACEHIEZ"
        };
        
        public override void Render(
            CommandBuffer cmd, 
            RenderTargetIdentifier src, 
            RenderTargetIdentifier dest,
            Camera camera)
        {
            if (m_GenerateHizComputeShader == null) {
                Debug.LogError("GenerateHizComputeShader is missing");
                return;
            }
            
            if (m_Material == null) {
                m_Material = CoreUtils.CreateEngineMaterial(Shader.Find(m_SSRShaderName));
            }
            
            if (m_SSRMode == SSRMode.ScreenSpaceHiz) {
                int width = camera.pixelWidth, height = camera.pixelHeight;
                m_HizCount = (uint)Mathf.Min(m_HizCount, Mathf.Log(Mathf.Min(width, height), 2));
                m_HizTextures = new RenderTexture[m_HizCount];
                m_PackHizTexture = RenderTexture.GetTemporary(
                    width, height, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
                m_PackHizTexture.useMipMap = true;
                m_PackHizTexture.autoGenerateMips = false;
                m_PackHizTexture.Create();
                
                RenderTargetIdentifier depthTex = CameraRenderer.m_CameraDepthTextureId;
                cmd.Blit(depthTex, m_PackHizTexture);
                // 需要手动生成，否则会被覆盖
                m_PackHizTexture.GenerateMips();
                
                width = width / 2;
                height = height / 2;
                
                // 生成并设置层次深度
                for (int i = 0; i < m_HizCount; ++i, width /= 2, height /= 2) {
                    m_HizTextures[i] = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.RFloat);
                    m_HizTextures[i].enableRandomWrite = true;
                    m_HizTextures[i].Create();
                    
                    cmd.SetComputeTextureParam(m_GenerateHizComputeShader, 0, m_HizTextureId, m_HizTextures[i]);
                    cmd.SetComputeTextureParam(m_GenerateHizComputeShader, 0, m_DepthTextureId, depthTex);
                    
                    cmd.DispatchCompute(m_GenerateHizComputeShader, 0, m_HizTextures[i].width, m_HizTextures[i].height, 1);

                    depthTex = m_HizTextures[i];
                    cmd.CopyTexture(depthTex, 0, 0, m_PackHizTexture, 0, i + 1);
                }
                
                m_Material.SetTexture(m_HizTextureId, m_PackHizTexture);
            }
            
            SetKeywords(cmd, m_SSRModeKeywords, (int)m_SSRMode - 1);
            
            m_Material.SetInt(m_HizCountId, (int)m_HizCount);
            m_Material.SetInt(m_HizStartLevelId, (int)Mathf.Min(m_HizStartLevel, m_HizCount));
            m_Material.SetInt(m_HizEndLevelId, (int)Mathf.Min(m_HizEndLevel, m_HizStartLevel));
            m_Material.SetInt(m_RayMarchingMaxCountId, m_RayMarchingMaxDistance);
            m_Material.SetFloat(m_RayMarchingStepId, m_RayMarchingStep);
            m_Material.SetFloat(m_HitThresholdId, m_HitThreshold);
            cmd.SetGlobalTexture(CameraRenderer.m_CameraColorTextureId, src);
            cmd.SetRenderTarget(dest, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
                //CameraRender.m_CameraDepthTextureId, RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);
            cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);


            if (m_SSRMode == SSRMode.ScreenSpaceHiz) {
                for (int i = 0; i < m_HizTextures.Length; ++i) {
                    RenderTexture.ReleaseTemporary(m_HizTextures[i]);
                }
                RenderTexture.ReleaseTemporary(m_PackHizTexture);
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