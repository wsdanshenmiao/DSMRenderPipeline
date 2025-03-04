using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    public class DSMRenderPipeline : RenderPipeline
    {
        private CameraRender m_CameraRender = new CameraRender();

        private bool m_UseDynamicBatching, m_UseGPUInstancing;

        /// <summary>
        /// 需要设置优化策略
        /// </summary>
        public DSMRenderPipeline(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher)
        {
            m_UseDynamicBatching = useDynamicBatching;
            m_UseGPUInstancing = useGPUInstancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach(Camera camera in cameras) {
                m_CameraRender.Render(context, camera, m_UseDynamicBatching, m_UseGPUInstancing);
            }
        }
    }
}
