using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM
{
    public class DSMRenderPipeline : RenderPipeline
    {
        private readonly CameraRenderer m_CameraRenderer;

        private DSMRenderPipelineSettings m_Settings;

        private readonly RenderGraph m_RenderGraph = new RenderGraph("DSM SRP Render Graph"); 
        
        /// <summary>
        /// 需要设置优化策略
        /// </summary>
        public DSMRenderPipeline(DSMRenderPipelineSettings settings)
        {
            m_Settings = settings;
            GraphicsSettings.useScriptableRenderPipelineBatching = settings.m_UseSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
            m_CameraRenderer = new CameraRenderer(settings.m_DebugShader);
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach(Camera camera in cameras) {
                m_CameraRenderer.Render(m_RenderGraph, context, camera, m_Settings);
            }
            m_RenderGraph.EndFrame();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            m_RenderGraph.Cleanup();
            m_CameraRenderer.Dispose();
        }
    }
}
