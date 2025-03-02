using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    public class DSMRenderPipeline : RenderPipeline
    {
        CameraRender m_CameraRender = new CameraRender();


        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach(Camera camera in cameras) {
                m_CameraRender.Render(context, camera);
            }
        }
    }
}
