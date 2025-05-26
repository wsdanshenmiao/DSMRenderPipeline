using UnityEditor;
using UnityEngine;

namespace DSM {
    [System.Serializable]
    public class DSMRenderPipelineSettings
    {
        // 可选优化方式
        public bool 
            m_UseDynamicBatching = true, 
            m_UseGPUInstancing = true, 
            m_UseSRPBatcher = true;

        public ShadowSetting m_ShadowSetting = default;

        public PostEffectManager m_PostEffectManager = new PostEffectManager();
    }
}