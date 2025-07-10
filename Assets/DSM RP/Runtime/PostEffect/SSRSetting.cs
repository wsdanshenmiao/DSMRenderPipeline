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
        [Range(1, 5)] public uint m_HizStride = 1;
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
            SSRPass.Record(
                renderGraph,
                cullingResults,
                camera,
                cameraTextures,
                target,
                this);
        }
    }
}