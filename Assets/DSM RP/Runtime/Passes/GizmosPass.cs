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
        static readonly ProfilingSampler m_Sampler = new ProfilingSampler("Gizmos");

        Camera m_Camera;

        private void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;
            ScriptableRenderContext renderContext = context.renderContext;

            renderContext.DrawGizmos(m_Camera, GizmoSubset.PreImageEffects);
            renderContext.DrawGizmos(m_Camera, GizmoSubset.PostImageEffects);
        }
#endif
        [Conditional("UNITY_EDITOR")]
        public static void Record(
            RenderGraph renderGraph,
            Camera camera)
        {
#if UNITY_EDITOR
            if (Handles.ShouldRenderGizmos())
            {
                using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                    m_Sampler.name, out GizmosPass pass, m_Sampler);
                pass.m_Camera = camera;
                builder.SetRenderFunc<GizmosPass>(
                    static (pass, context) => pass.Render(context));
            }
#endif
        }
    }


}
