using System.Diagnostics;
using DSM;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

public class DebugPass
{
	static readonly ProfilingSampler sampler = new("Debug");

	[Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
	public static void Record(
		RenderGraph renderGraph,
		DSMRenderPipelineSettings settings,
		Camera camera,
		in LightResources lightData)
	{
		if (CameraDebugger.IsActive &&
			camera.cameraType <= CameraType.SceneView)
		{
			using RenderGraphBuilder builder = renderGraph.AddRenderPass(
				sampler.name, out DebugPass pass, sampler);
			builder.ReadBuffer(lightData.m_TileLightIndicesBuffer);
			builder.SetRenderFunc<DebugPass>(static (pass, context) => CameraDebugger.Render(context));
		}
	}
}