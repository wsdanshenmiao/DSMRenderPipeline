using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM
{
    public partial class CameraRenderer
    {
        public CameraRenderer(Shader debugShader)
        {
            CameraDebugger.Initialize(debugShader);
        }

        public void Dispose()
        {
            CameraDebugger.Cleanup();
        }

        public void Render(
            RenderGraph renderGraph,
            ScriptableRenderContext context,
            Camera camera,
            DSMRenderPipelineSettings settings)
        {
            if (context == null || camera == null)
            {
                Debug.LogError("Context or Camera should no be null");
                return;
            }

            ShadowSetting shadowSetting = settings.m_ShadowSetting;
            RenderingLayerMask renderingLayerMask = settings.m_RenderLayerMask;

            if (!camera.TryGetCullingParameters(
                out ScriptableCullingParameters cullingParameters)) return;
            cullingParameters.shadowDistance = Mathf.Min(
                camera.farClipPlane, shadowSetting.m_MaxDistance);
            CullingResults cullingResults = context.Cull(ref cullingParameters);

            ProfilingSampler cameraSampler = ProfilingSampler.Get(camera.cameraType);

            var renderGraphParams = new RenderGraphParameters
            {
                executionName = cameraSampler.name,
                currentFrameIndex = Time.frameCount,
                rendererListCulling = true,
                scriptableRenderContext = context,
                commandBuffer = CommandBufferPool.Get()
            };

            Vector2Int attachmentSize = new Vector2Int(camera.pixelWidth, camera.pixelHeight);

            renderGraph.BeginRecording(renderGraphParams);
            using (new RenderGraphProfilingScope(renderGraph, cameraSampler))
            {
                LightResources lightResources = LightingPass.Record(
                    renderGraph, cullingResults,
                    shadowSetting, context, attachmentSize, settings.m_RenderLayerMask);

                var cameraRendererTexs = SetupPass.Record(
                    renderGraph, camera, true, true);

                GeometryPass.Record(
                    renderGraph, cullingResults, camera, renderingLayerMask,
                    true, cameraRendererTexs, lightResources);

                SkyboxPass.Record(renderGraph, camera, cameraRendererTexs);

                GeometryPass.Record(
                    renderGraph, cullingResults, camera, renderingLayerMask,
                    false, cameraRendererTexs, lightResources);
                
                settings.m_PostEffectManager.Record(
                    renderGraph, cullingResults, camera, context, cameraRendererTexs);

                UnsupportedShadersPass.Record(renderGraph, cullingResults, camera);

                FinalPass.Record(renderGraph, cameraRendererTexs);

                DebugPass.Record(renderGraph, settings, camera, lightResources);
                GizmosPass.Record(renderGraph, camera, cameraRendererTexs);
            }
            renderGraph.EndRecordingAndExecute();

            context.ExecuteCommandBuffer(renderGraphParams.commandBuffer);
            context.Submit();
            CommandBufferPool.Release(renderGraphParams.commandBuffer);

        }
    }
}
