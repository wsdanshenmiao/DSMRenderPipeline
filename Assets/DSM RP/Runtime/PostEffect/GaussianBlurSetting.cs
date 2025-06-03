using UnityEngine;

namespace DSM
{
    [CreateAssetMenu(menuName = "Rendering/Custom PostEffect/GaussianBlur")]
    public class GaussianBlurSetting : PostEffectSetting
    {
        private GaussianBlurPass m_Pass = null;
        
        [Range(0, 50)] public uint m_BlurRadius = 5;
        public ComputeShader m_BlurShader = null;
        
        public override PostEffect GetPostEffect()
        {
            if(m_Pass == null) {
                m_Pass = new GaussianBlurPass();
                m_Pass.sm_Setting = this;
            }
            return m_Pass;
        }
    }

}