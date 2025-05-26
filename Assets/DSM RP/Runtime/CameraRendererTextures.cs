using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM {
    /// <summary>
    /// 用于管理相机相关的资源
    /// </summary>
    public readonly ref struct CameraRendererTextures
    {
        public readonly TextureHandle
            m_ColorTexture, m_DepthTexture,
            m_ColorCopy, m_DepthCopy;

        public CameraRendererTextures(
            TextureHandle colorTex,
            TextureHandle depthTex,
            TextureHandle colorCopy,
            TextureHandle depthCopy)
        {
            m_ColorTexture = colorTex;
            m_DepthTexture = depthTex;
            m_ColorCopy = colorCopy;
            m_DepthCopy = depthCopy;
        }
    }
}