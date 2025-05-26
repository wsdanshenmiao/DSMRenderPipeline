using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM {
    /// <summary>
    /// 创建渲染相关资源并绑定到管线
    /// </summary>
    public class SetupPass
    {
        private static readonly ProfilingSampler sm_Sampler = new ProfilingSampler("Setup");

        public static readonly int
            m_CameraTextureSizeId = Shader.PropertyToID("_CameraTextureSize");

        TextureHandle m_ColorTexture, m_DepthTexture, m_NormalTexture;

        Camera m_Camera;

        private void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;
            var renderContext = context.renderContext;

            // 提前设置相机的属性
            renderContext.SetupCameraProperties(m_Camera);

            CameraClearFlags flags = m_Camera.clearFlags;
            flags = flags > CameraClearFlags.Color ? CameraClearFlags.Color : flags;    // 确保临时纹理被清理

            RenderTargetIdentifier[] renderTargets = {
                m_ColorTexture, m_NormalTexture
            };

            cmd.SetRenderTarget(renderTargets, m_DepthTexture);

            RTClearFlags clearFlags = RTClearFlags.All;
            if (flags == CameraClearFlags.Nothing)
            {
                clearFlags = RTClearFlags.None;
            }
            else if (flags == CameraClearFlags.Depth)
            {
                clearFlags = RTClearFlags.DepthStencil;
            }
            Color[] clearColors = {
                flags == CameraClearFlags.Color ?
                    m_Camera.backgroundColor.linear : Color.clear,
                Color.clear
            };
            cmd.ClearRenderTarget(clearFlags, clearColors);
            Vector2Int WH = new Vector2Int(m_Camera.pixelWidth, m_Camera.pixelHeight);
            Vector4 texSize = new Vector4(1f / WH.x, 1f / WH.y, WH.x, WH.y);
            cmd.SetGlobalVector(m_CameraTextureSizeId, texSize);
            
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public static CameraRendererTextures Record(
            RenderGraph renderGraph,
            Camera camera,
            bool copyColor,
            bool copyDepth)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sm_Sampler.name, out SetupPass pass, sm_Sampler);

            pass.m_Camera = camera;

            Vector2Int WH = new Vector2Int(camera.pixelWidth, camera.pixelHeight);

            TextureHandle colorCopy = default, depthCopy = default;

            TextureDesc texDesc = new TextureDesc(WH.x, WH.y)
            {
                colorFormat = SystemInfo.GetGraphicsFormat(DefaultFormat.LDR),
                name = "CameraColorTexture"
            };
            TextureHandle colorTex = pass.m_ColorTexture = 
                builder.WriteTexture(renderGraph.CreateTexture(texDesc));

            if (copyColor)
            {
                texDesc.name = "CameraColorTextureCopy";
                colorCopy = renderGraph.CreateTexture(texDesc);
            }

            texDesc.depthBufferBits = DepthBits.Depth32;
            texDesc.name = "CameraDepthTexture";
            TextureHandle depthTex = pass.m_DepthTexture =
                builder.WriteTexture(renderGraph.CreateTexture(texDesc));

            if (copyDepth)
            {
                texDesc.name = "CameraDepthTextureCopy";
                depthCopy = renderGraph.CreateTexture(texDesc);
            }

            texDesc.format = GraphicsFormat.R32_SFloat;
            texDesc.name = "NormalTexture";
            TextureHandle normalTex = pass.m_NormalTexture =
                builder.WriteTexture(renderGraph.CreateTexture(texDesc));

            builder.AllowPassCulling(false);
            builder.SetRenderFunc<SetupPass>(
                static (pass, context) => pass.Render(context));

            return new CameraRendererTextures(
                colorTex, depthTex, normalTex, colorCopy, depthCopy);
        }
    }
}