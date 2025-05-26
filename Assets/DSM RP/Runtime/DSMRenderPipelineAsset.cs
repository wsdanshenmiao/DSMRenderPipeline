using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    [CreateAssetMenu(menuName = "DSMRendering/DSM Render Pipeline")]
    public class DSMRenderPipelineAssets : RenderPipelineAsset
    {
        [SerializeField]
        private DSMRenderPipelineSettings m_Settings;

        protected override RenderPipeline CreatePipeline()
        {
            return new DSMRenderPipeline(m_Settings);
        }
    }
}
