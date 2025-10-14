using System.Runtime.InteropServices;
using Unity.Collections;
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
        // 指定结构体的布局，防止编译器优化
        [StructLayout(LayoutKind.Sequential)]
        struct DirectionalLightData
        {
            // 常量不占用内存
            public const int stride = 4 * 4 * 3;

            public Vector4 color, direction, shadowData;

            public DirectionalLightData(ref VisibleLight visibleLight, Vector4 _shadowData)
            {
                color = visibleLight.finalColor;
                direction = -visibleLight.localToWorldMatrix.GetColumn(2);
                shadowData = _shadowData;
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        struct OtherLightData
        {
            // 常量不占用内存
            public const int stride = 5 * 4 * 4;

            public Vector4 color, direction, positionAndRange, shadowData, spotAngle;

            public static OtherLightData CreatePointLight(ref VisibleLight visibleLight, Vector4 _shadowData)
            {
                OtherLightData ret;
                ret.color = visibleLight.finalColor;
                ret.direction = Vector4.zero;
                ret.positionAndRange = visibleLight.localToWorldMatrix.GetColumn(3);
                // 将光源的范围储存在 w 分量
                ret.positionAndRange.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
                ret.shadowData = _shadowData;
                ret.spotAngle = new Vector4(Mathf.PI * 0.5f, Mathf.PI, 0f, 0f);
                return ret;
            }
            public static OtherLightData CreateSpotLight(ref VisibleLight visibleLight, Vector4 _shadowData)
            {
                OtherLightData ret;
                ret.color = visibleLight.finalColor;
                ret.direction = visibleLight.localToWorldMatrix.GetColumn(2);
                ret.positionAndRange = visibleLight.localToWorldMatrix.GetColumn(3);
                // 将光源的范围储存在 w 分量
                ret.positionAndRange.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.0001f);
                ret.shadowData = _shadowData;
                float angle = visibleLight.spotAngle;
                ret.spotAngle = new Vector4(0, angle, 0f, 0f);
                return ret;
            }
        }

        private static readonly ProfilingSampler sm_Sampler = new ProfilingSampler("Lighting");

        private CullingResults m_CullingResults;

        private const int m_MaxDirLightCount = 4;
        private const int m_MaxOtherLightCount = 128;

        private int m_DirLightCount, m_OtherLightCount;

        private Shadows m_Shadows = new Shadows();

        private BufferHandle m_DirLightDataBuffer;
        private BufferHandle m_OtherLightDataBuffer;

        static readonly private int
            m_DirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            m_DirLightDataId = Shader.PropertyToID("_DirectionalLightDatas");

        static readonly private int
            m_OtherLightCountId = Shader.PropertyToID("_OtherLightCount"),
            m_OtherLightDatasId = Shader.PropertyToID("_OtherLightDatas");

        static private DirectionalLightData[] m_DirLightDatas = new DirectionalLightData[m_MaxDirLightCount];

        static private OtherLightData[] m_OtherLightDatas = new OtherLightData[m_MaxOtherLightCount];

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

            m_DirLightCount = m_OtherLightCount = 0;
            for (int index = 0; index < visibleLights.Length; ++index)
            {
                VisibleLight visibleLight = visibleLights[index];
                Light light = visibleLight.light;
                if ((light.renderingLayerMask & renderLayerMask) == 0) continue;

                switch (visibleLight.lightType)
                {
                    case LightType.Directional:
                        m_DirLightDatas[m_DirLightCount++] = new DirectionalLightData(
                            ref visibleLight, m_Shadows.ReserveDirectionalShadows(light, index));
                        break;
                    case LightType.Point:
                        m_OtherLightDatas[m_OtherLightCount++] = OtherLightData.CreatePointLight(
                            ref visibleLight, Vector4.zero);
                        break;
                    case LightType.Spot:
                        m_OtherLightDatas[m_OtherLightCount++] = OtherLightData.CreateSpotLight(
                            ref visibleLight, Vector4.zero);
                        break;
                    default: break;
                }
            }
        }

        // 提交渲染命令
        private void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;
            ScriptableRenderContext renderContext = context.renderContext;

            m_Shadows.Render(context);

            cmd.SetGlobalInt(m_DirLightCountId, m_DirLightCount);
            if (m_DirLightCount > 0) {
                cmd.SetBufferData(m_DirLightDataBuffer, m_DirLightDatas);
                cmd.SetGlobalBuffer(m_DirLightDataId, m_DirLightDataBuffer);
            }
            cmd.SetGlobalInt(m_OtherLightCountId, m_OtherLightCount);
            if (m_OtherLightCountId > 0) {
                cmd.SetBufferData(m_OtherLightDataBuffer, m_OtherLightDatas);
                cmd.SetGlobalBuffer(m_OtherLightDatasId, m_OtherLightDataBuffer);
            }

            context.renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        // 分配所需的资源
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

            // 创建光照数据的结构缓冲区
            pass.m_DirLightDataBuffer = renderGraph.CreateBuffer(
                new BufferDesc(m_MaxDirLightCount, DirectionalLightData.stride)
                {
                    name = "Directional Light Data Buffer"
                });
            pass.m_OtherLightDataBuffer = renderGraph.CreateBuffer(
                new BufferDesc(m_MaxOtherLightCount, OtherLightData.stride)
                {
                    name = "Other Light Data Buffer"
                });

            builder.WriteBuffer(pass.m_DirLightDataBuffer);
            builder.WriteBuffer(pass.m_OtherLightDataBuffer);

            builder.SetRenderFunc<LightingPass>(
                static (pass, context) => pass.Render(context));
            builder.AllowPassCulling(false);

            return new LightResources(
                pass.m_Shadows.GetResources(renderGraph, builder, renderContext));
        }
    }
}