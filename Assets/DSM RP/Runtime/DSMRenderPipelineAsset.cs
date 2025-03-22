using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    [CreateAssetMenu(menuName = "DSMRendering/DSM Render Pipeline")]
    public class DSMRenderPipelineAssets : RenderPipelineAsset
    {
        // 可选优化方式
        [SerializeField]
        private bool m_UseDynamicBatching = true, m_UseGPUInstancing = true, m_UseSRPBatcher = true;

        [SerializeField] private ShadowSetting m_ShadowSetting = default;
        
        protected override RenderPipeline CreatePipeline()
        {
            return new DSMRenderPipeline(
                m_UseDynamicBatching, 
                m_UseGPUInstancing, 
                m_UseSRPBatcher,
                m_ShadowSetting);
        }
    }
}
