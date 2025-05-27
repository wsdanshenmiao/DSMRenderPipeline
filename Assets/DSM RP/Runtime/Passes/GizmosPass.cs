using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;


namespace DSM{


    public class GizmosPass
    {
#if UNITY_EDITOR
        static readonly ProfilingSampler sm_Sampler = new ProfilingSampler("Gizmos");

        private Camera m_Camera;

        private TextureHandle m_DepthTexture;

        private void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;
            ScriptableRenderContext renderContext = context.renderContext;
/*            cmd.Blit(m_DepthTexture, BuiltinRenderTextureType.CameraTarget);
            renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();*/
            renderContext.DrawGizmos(m_Camera, GizmoSubset.PreImageEffects);
            renderContext.DrawGizmos(m_Camera, GizmoSubset.PostImageEffects);
        }
#endif
        [Conditional("UNITY_EDITOR")]
        public static void Record(
            RenderGraph renderGraph,
            Camera camera,
            in CameraRendererTextures cameraTextures)
        {
#if UNITY_EDITOR
            if (Handles.ShouldRenderGizmos())
            {
                using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                    sm_Sampler.name, out GizmosPass pass, sm_Sampler);
                pass.m_Camera = camera;
                pass.m_DepthTexture = builder.ReadTexture(cameraTextures.m_DepthTexture);
                builder.SetRenderFunc<GizmosPass>(
                    static (pass, context) => pass.Render(context));
            }
#endif
        }
    }


}
