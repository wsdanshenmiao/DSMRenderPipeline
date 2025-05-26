using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM { 

    public readonly ref struct LightResources
    {
        public readonly ShadowResources m_ShadowResources;

        public LightResources(ShadowResources shadowResources)
        {
            m_ShadowResources = shadowResources;
        }
    }

    public class LightingPass
    {
        private static readonly ProfilingSampler sm_Sampler = new ProfilingSampler("Lighting");

        private CullingResults m_CullingResults;
        private const int m_MaxDirLightCount = 4;

        private int m_DirLightCount;

        Shadows m_Shadows = new Shadows();

        static readonly private int
            m_DirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            m_DirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
            m_DirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
            m_DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");


        static private Vector4[]
            m_DirLightColors = new Vector4[m_MaxDirLightCount],
            m_DirLightDirections = new Vector4[m_MaxDirLightCount],
            m_DirLightShadowData = new Vector4[m_MaxDirLightCount];

        private void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;
            ScriptableRenderContext renderContext = context.renderContext;

            m_Shadows.Render(context);

            cmd.SetGlobalInt(m_DirLightCountId, m_DirLightCount);
            cmd.SetGlobalVectorArray(m_DirLightColorsId, m_DirLightColors);
            cmd.SetGlobalVectorArray(m_DirLightDirectionsId, m_DirLightDirections);
            cmd.SetGlobalVectorArray(m_DirLightShadowDataId, m_DirLightShadowData);

            context.renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        private void Setup(
            CullingResults cullingResults, 
            ShadowSetting shadowSetting,
            uint renderLayerMask)
        {
            m_CullingResults = cullingResults;
            m_Shadows.Setup(cullingResults, shadowSetting);
            SetupLights(renderLayerMask);
        }

        private void SetupLights(uint renderLayerMask)
        {
            NativeArray<VisibleLight> visibleLights = m_CullingResults.visibleLights;
            m_DirLightCount = Mathf.Min(visibleLights.Length, m_MaxDirLightCount);
            for (int i = 0; i < m_DirLightCount; ++i)
            {
                VisibleLight visibleLight = visibleLights[i];
                Light light = visibleLight.light;
                if ((light.renderingLayerMask & renderLayerMask) == 0) continue;

                if (visibleLight.lightType == LightType.Directional)
                {
                    SetupDirectionalLight(i, ref visibleLight);
                }
            }
        }

        private void SetupDirectionalLight(int index, ref VisibleLight light)
        {
            m_DirLightColors[index] = light.finalColor;
            m_DirLightDirections[index] = -light.localToWorldMatrix.GetColumn(2);
            m_DirLightShadowData[index] = m_Shadows.ReserveDirectionalShadows(light.light, index);
        }

        public static LightResources Record(
            RenderGraph renderGraph,
            CullingResults cullingResults,
            ShadowSetting shadowSetting,
            ScriptableRenderContext renderContext,
            uint renderLayerMask)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sm_Sampler.name, out LightingPass pass, sm_Sampler);
            pass.Setup(cullingResults, shadowSetting, renderLayerMask);

            builder.SetRenderFunc<LightingPass>(
                static (pass, context) => pass.Render(context));
            builder.AllowPassCulling(false);

            return new LightResources(
                pass.m_Shadows.GetResources(renderGraph, builder, renderContext));
        }
    }
}