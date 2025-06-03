using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM
{
    [CreateAssetMenu(menuName = "Rendering/Custom PostEffect/GaussianBlur")]
    public class GaussianBlurSetting : PostEffectSetting
    {
        [Range(0, 50)] public uint m_BlurRadius = 5;
        public ComputeShader m_BlurShader = null;

        public override void Record(
             RenderGraph renderGraph,
             CullingResults cullingResults,
             Camera camera,
             in CameraRendererTextures cameraTextures,
             TextureHandle target)
        {
            GaussianBlurPass.Record(
                renderGraph,
                m_BlurShader,
                target,
                m_BlurRadius,
                camera.pixelWidth,
                camera.pixelHeight);
        }
    }

}