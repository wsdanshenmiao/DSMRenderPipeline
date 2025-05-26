using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine;
using System.Diagnostics;

public class UnsupportedShadersPass
{
#if UNITY_EDITOR
    private static readonly ProfilingSampler sm_Sampler = new("Unsupported Shaders");

    private static readonly ShaderTagId[] m_ShaderTagIDs = {
        new("Always"),
        new("ForwardBase"),
        new("PrepassBase"),
        new("Vertex"),
        new("VertexLMRGBM"),
        new("VertexLM")
    };

    private static Material m_ErrorMaterial;

    private RendererListHandle m_RenderList;

    void Render(RenderGraphContext context)
    {
        context.cmd.DrawRendererList(m_RenderList);
        context.renderContext.ExecuteCommandBuffer(context.cmd);
        context.cmd.Clear();
    }
#endif

    [Conditional("UNITY_EDITOR")]
    public static void Record(
        RenderGraph renderGraph, 
        CullingResults cullingResults, 
        Camera camera)
    {
#if UNITY_EDITOR
        using RenderGraphBuilder builder = renderGraph.AddRenderPass(
            sm_Sampler.name, out UnsupportedShadersPass pass, sm_Sampler);

        if (m_ErrorMaterial == null)
        {
            m_ErrorMaterial = new(Shader.Find("Hidden/InternalErrorShader"));
        }

        pass.m_RenderList = builder.UseRendererList(renderGraph.CreateRendererList(
            new RendererListDesc(m_ShaderTagIDs, cullingResults, camera)
            {
                overrideMaterial = m_ErrorMaterial,
                renderQueueRange = RenderQueueRange.all
            }));

        builder.SetRenderFunc<UnsupportedShadersPass>(
            static (pass, context) => pass.Render(context));
#endif
    }
}