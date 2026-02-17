using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM {

    public readonly ref struct LightResources
    {
        public readonly ShadowResources m_ShadowResources;
        public readonly BufferHandle m_TileLightIndicesBuffer;

        public LightResources(ShadowResources shadowResources, 
            BufferHandle tileLightIndicesBuffer)
        {
            m_ShadowResources = shadowResources;
            m_TileLightIndicesBuffer = tileLightIndicesBuffer;
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

        // Forward+ data
        private int m_MaxLightPreTile;
        private int m_MaxTileDataSize;
        private int m_TileDataSize; // 第一个元素存储每个瓦片的光源数量
        private const int m_TileScreenPixelSize = 64; // 每个瓦片覆盖的屏幕像素大小
        private Vector2Int m_TileSize;
        private int TileCount => m_TileSize.x * m_TileSize.y;
        private Vector2 m_ScreenUVToTileCoordinates;
        private BufferHandle m_TileLightIndicesBuffer;
        private float4[] m_LightBounds = new float4[m_MaxOtherLightCount];  // 光照范围
        private int[] m_TileBufferData;


        // Shader hash IDs
        static readonly private int
            m_DirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            m_DirLightDataId = Shader.PropertyToID("_DirectionalLightDatas");

        static readonly private int
            m_OtherLightCountId = Shader.PropertyToID("_OtherLightCount"),
            m_OtherLightDatasId = Shader.PropertyToID("_OtherLightDatas");

        static readonly private int
            m_TileLightIndicesId = Shader.PropertyToID("_TileLightIndices"),
            m_TileSettingId = Shader.PropertyToID("_TileSettings");

        static private DirectionalLightData[] m_DirLightDatas = new DirectionalLightData[m_MaxDirLightCount];
        static private OtherLightData[] m_OtherLightDatas = new OtherLightData[m_MaxOtherLightCount];

        private void Setup(
            CullingResults cullingResults,
            ShadowSetting shadowSetting,
            ForwardPlusSettings forwardPlusSettings,
            Vector2Int attachmentSize,
            uint renderLayerMask)
        {
            m_CullingResults = cullingResults;
            m_Shadows.Setup(cullingResults, shadowSetting);
            
            m_MaxLightPreTile = forwardPlusSettings.m_MaxLightsPerTile <= 0 ?
                31 : forwardPlusSettings.m_MaxLightsPerTile;
            m_MaxTileDataSize = m_MaxLightPreTile + 1;
            float tileScreenPixelSize = forwardPlusSettings.m_TileSize <= 0 ? 
                64 : (float)forwardPlusSettings.m_TileSize;
            m_ScreenUVToTileCoordinates = new Vector2(
                attachmentSize.x / tileScreenPixelSize,
                attachmentSize.y / tileScreenPixelSize);
            m_TileSize = new Vector2Int(
                Mathf.CeilToInt(m_ScreenUVToTileCoordinates.x),
                Mathf.CeilToInt(m_ScreenUVToTileCoordinates.y));

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
                    {
                        if(m_DirLightCount < m_MaxDirLightCount)
                        {
                            m_DirLightDatas[m_DirLightCount++] = new DirectionalLightData(
                                ref visibleLight, m_Shadows.ReserveDirectionalShadows(light, index));   
                        }
                        break;
                    }
                    case LightType.Point:
                    {
                        if (m_OtherLightCount < m_MaxOtherLightCount)
                        {
                            Rect rect = visibleLight.screenRect;
                            m_LightBounds[m_OtherLightCount] = new float4(
                                rect.xMin, rect.yMin, rect.xMax, rect.yMax);
                            m_OtherLightDatas[m_OtherLightCount++] = OtherLightData.CreatePointLight(
                                ref visibleLight, Vector4.zero);
                        }
                        break;
                    }
                    case LightType.Spot:
                    {
                        if(m_OtherLightCount < m_MaxOtherLightCount)
                        {
                            Rect rect = visibleLight.screenRect;
                            m_LightBounds[m_OtherLightCount] = new float4(
                                rect.xMin, rect.yMin, rect.xMax, rect.yMax);
                            m_OtherLightDatas[m_OtherLightCount++] = OtherLightData.CreateSpotLight(
                                ref visibleLight, Vector4.zero);
                        }
                        break;
                    }
                    default: break;
                }
            }

            int requiredMaxLightPreTile = Mathf.Min(m_MaxLightPreTile, visibleLights.Length);
            m_TileDataSize = requiredMaxLightPreTile + 1;
            m_TileBufferData = new int[m_TileDataSize * TileCount];
            float2 tileScreenSize = new Vector2(1, 1) / m_ScreenUVToTileCoordinates;
            for(int tileIndex = 0; tileIndex < TileCount; ++tileIndex)
            {
                int x = tileIndex % m_TileSize.x;
                int y = tileIndex / m_TileSize.x;
                float4 bound = new float4(x, y, x + 1, y + 1) * tileScreenSize.xyxy;

                int start = tileIndex * m_TileDataSize;
                int offset = start;
                int lightCount = 0; // 当前 tile 内的光源数量
                for(int i = 0; i < m_OtherLightCount; i++)
                {
                    float4 lightBound = m_LightBounds[i];
                    // 判断是否在边界内
                    if(math.all(new float4(lightBound.xy, bound.xy) <= new float4(bound.zw, lightBound.zw)))
                    {
                        m_TileBufferData[++offset] = i;
                        if(++lightCount >= requiredMaxLightPreTile)
                        {
                            break;
                        }
                    }
                }
                m_TileBufferData[start] = lightCount;
            }
        }

        // 提交渲染命令
        private void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;

            m_Shadows.Render(context);

            cmd.SetGlobalInt(m_DirLightCountId, m_DirLightCount);
            if (m_DirLightCount > 0) {
                cmd.SetBufferData(m_DirLightDataBuffer, m_DirLightDatas);
                cmd.SetGlobalBuffer(m_DirLightDataId, m_DirLightDataBuffer);
            }

            cmd.SetGlobalInt(m_OtherLightCountId, m_OtherLightCount);
            if (m_OtherLightCount > 0) {
                cmd.SetBufferData(m_OtherLightDataBuffer, m_OtherLightDatas);
                cmd.SetGlobalBuffer(m_OtherLightDatasId, m_OtherLightDataBuffer);

                cmd.SetGlobalVector(m_TileSettingId, new Vector4(
                    m_ScreenUVToTileCoordinates.x,
                    m_ScreenUVToTileCoordinates.y,
                    m_TileSize.x, m_TileDataSize));
                cmd.SetBufferData(m_TileLightIndicesBuffer, m_TileBufferData);
                cmd.SetGlobalBuffer(m_TileLightIndicesId, m_TileLightIndicesBuffer);
            }

            context.renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        // 分配所需的资源
        public static LightResources Record(
            RenderGraph renderGraph,
            CullingResults cullingResults,
            ShadowSetting shadowSetting,
            ForwardPlusSettings forwardPlusSettings,
            ScriptableRenderContext renderContext,
            Vector2Int attachmentSize,
            uint renderLayerMask)
        {
            using RenderGraphBuilder builder = renderGraph.AddRenderPass(
                sm_Sampler.name, out LightingPass pass, sm_Sampler);
            pass.Setup(cullingResults, shadowSetting, forwardPlusSettings, attachmentSize, renderLayerMask);

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
            pass.m_TileLightIndicesBuffer = renderGraph.CreateBuffer(
                new BufferDesc(1, sizeof(uint))
                {
                    name = "Tile Light Indices Buffer",
                    count = pass.TileCount * pass.m_MaxTileDataSize,
                    stride = 4
                });

            builder.WriteBuffer(pass.m_DirLightDataBuffer);
            builder.WriteBuffer(pass.m_OtherLightDataBuffer);
            builder.WriteBuffer(pass.m_TileLightIndicesBuffer);

            builder.SetRenderFunc<LightingPass>(
                static (pass, context) => pass.Render(context));
            builder.AllowPassCulling(false);

            return new LightResources(
                pass.m_Shadows.GetResources(renderGraph, builder, renderContext),
                pass.m_TileLightIndicesBuffer);
        }
    }
}