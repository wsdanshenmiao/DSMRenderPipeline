using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    [CreateAssetMenu(menuName = "DSMRendering/DSM Render Pipeline")]
    public class DSMRenderPipelineAssets : RenderPipelineAsset
    {
        protected override RenderPipeline CreatePipeline()
        {
            return new DSMRenderPipeline();
        }
    }
}
