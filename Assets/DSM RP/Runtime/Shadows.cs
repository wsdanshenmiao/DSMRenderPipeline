using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    /// <summary>
    /// 阴影类，用于管理阴影
    /// </summary>
    public class Shadows
    {
        struct ShadowedDirectionalLight
        {
            public int m_VisibleLightIndex;
        }
        
        private const string m_BufferName = "Shadows";
        private CommandBuffer m_CommandBuffer = new CommandBuffer{name=m_BufferName};
        
        private ScriptableRenderContext m_RenderContext;
        private CullingResults m_CullingResults;
        private ShadowSetting m_ShadowSetting;

        // 可生成阴影的最大方向光数量
        private const int m_MaxShadowedDirectionalLightCount = 4;
        private int m_ShadowedDirectionalLightCount;
        
        // 可生成阴影的光源的索引
        private ShadowedDirectionalLight[] m_ShadowedDirectionalLights = 
                new ShadowedDirectionalLight[m_MaxShadowedDirectionalLightCount];
        
        static int m_DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");

        public void Setup(
            ScriptableRenderContext renderContext,
            CullingResults cullingResults,
            ShadowSetting shadowSetting)
        {
            m_RenderContext = renderContext;
            m_CullingResults = cullingResults;
            m_ShadowSetting = shadowSetting;
            m_ShadowedDirectionalLightCount = 0;
        }

        public void ReserveDirectionalShadows(Light light, int visibleLightIndex)
        {
            // 当该光源不需要阴影 或 阴影强度为0 或 光源超出范围时不生成阴影
            if (m_ShadowedDirectionalLightCount < m_MaxShadowedDirectionalLightCount &&
                light.shadows != LightShadows.None && light.shadowStrength > 0 &&
                m_CullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)) {
                m_ShadowedDirectionalLights[m_ShadowedDirectionalLightCount++] =
                    new ShadowedDirectionalLight();
            }
        }

        public void Render()
        {
            Debug.Log("Rendering shadows");
            if (m_ShadowedDirectionalLightCount > 0) {
                RenderDirectionalShadows();    
            }
            else {
                m_CommandBuffer.GetTemporaryRT(m_DirShadowAtlasId, 1, 1, 
                    32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            }
        }

        public void RenderDirectionalShadows()
        {
            // 获取ShadowMap的大小
            int atlasSize = (int)m_ShadowSetting.directional.atlasSize;
            // 创建一个32位双线性过滤的临时纹理
            m_CommandBuffer.GetTemporaryRT(m_DirShadowAtlasId, atlasSize, atlasSize, 
                32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            m_CommandBuffer.SetRenderTarget(
                m_DirShadowAtlasId, 
                RenderBufferLoadAction.DontCare,
                RenderBufferStoreAction.Store);
            m_CommandBuffer.ClearRenderTarget(true, false, Color.clear);
            m_CommandBuffer.BeginSample(m_BufferName);

            for (int i = 0; i < m_ShadowedDirectionalLightCount; i++) {
                RenderDirectionalShadows(i, atlasSize);
            }
            
            m_CommandBuffer.EndSample(m_BufferName);
            ExecuteBuffer();
        }
        
        // 渲染ShadowMap
        private void RenderDirectionalShadows(int index, int tileSize)
        {
            ShadowedDirectionalLight light = m_ShadowedDirectionalLights[index];
            ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(
                m_CullingResults, light.m_VisibleLightIndex, BatchCullingProjectionType.Orthographic);
            m_CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                light.m_VisibleLightIndex, 0, 1, Vector3.zero, tileSize, 0f,
                out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                out ShadowSplitData splitData);
            // 阴影分割的剔除信息
            shadowDrawingSettings.splitData = splitData;
            // 设置视图和投影矩阵
            m_CommandBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
            ExecuteBuffer();
            m_RenderContext.DrawShadows(ref shadowDrawingSettings);
        }

        public void CleanUp()
        {
            // 设置渲染对象并清理
            m_CommandBuffer.ReleaseTemporaryRT(m_DirShadowAtlasId);
            
            ExecuteBuffer();
        }

        public void ExecuteBuffer()
        {
            // 关闭并清理命令列表
            m_RenderContext.ExecuteCommandBuffer(m_CommandBuffer);
            m_CommandBuffer.Clear();
        }
    }
}