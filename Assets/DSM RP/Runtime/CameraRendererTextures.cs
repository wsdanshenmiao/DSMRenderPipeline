using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM {
    /// <summary>
    /// 用于管理相机相关的资源
    /// </summary>
    public readonly ref struct CameraRendererTextures
    {
        public static readonly int
            m_CameraColorTextureId = Shader.PropertyToID("_CameraColorTexture"),
            m_CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
            m_NormalTextureId = Shader.PropertyToID("_NormalTexture");

        public readonly TextureHandle
            m_ColorTexture, m_DepthTexture, m_NormalTexture,
            m_ColorCopy, m_DepthCopy;

        public CameraRendererTextures(
            TextureHandle colorTex,
            TextureHandle depthTex,
            TextureHandle normalTex,
            TextureHandle colorCopy,
            TextureHandle depthCopy)
        {
            m_ColorTexture = colorTex;
            m_DepthTexture = depthTex;
            m_NormalTexture = normalTex;
            m_ColorCopy = colorCopy;
            m_DepthCopy = depthCopy;
        }
    }
}