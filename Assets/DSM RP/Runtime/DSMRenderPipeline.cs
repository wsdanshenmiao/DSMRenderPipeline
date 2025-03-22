using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    public class DSMRenderPipeline : RenderPipeline
    {
        private CameraRender m_CameraRender = new CameraRender();

        private bool m_UseDynamicBatching, m_UseGPUInstancing;

        private ShadowSetting m_ShadowSetting;

        /// <summary>
        /// 需要设置优化策略
        /// </summary>
        public DSMRenderPipeline(
            bool useDynamicBatching, 
            bool useGPUInstancing, 
            bool useSRPBatcher,
            ShadowSetting shadowSetting)
        {
            m_UseDynamicBatching = useDynamicBatching;
            m_UseGPUInstancing = useGPUInstancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
            m_ShadowSetting = shadowSetting;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach(Camera camera in cameras) {
                m_CameraRender.Render(
                    context, 
                    camera, 
                    m_UseDynamicBatching, 
                    m_UseGPUInstancing,
                    m_ShadowSetting);
            }
        }
    }
}
