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
            public float m_SlopeScaleBias;
            public float m_NearPlaneOffset;
        }
        
        private const string m_BufferName = "Shadows";
        private CommandBuffer m_CommandBuffer = new CommandBuffer{name=m_BufferName};
        
        private ScriptableRenderContext m_RenderContext;
        private CullingResults m_CullingResults;
        private ShadowSetting m_ShadowSetting;

        // 可生成阴影的最大方向光数量
        private const int m_MaxShadowedDirectionalLightCount = 4, m_MaxCascades = 4;
        private int m_ShadowedDirectionalLightCount;
        
        // 可生成阴影的光源的索引
        private ShadowedDirectionalLight[] m_ShadowedDirectionalLights = 
                new ShadowedDirectionalLight[m_MaxShadowedDirectionalLightCount];
        

        private static int
            m_DirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
            m_DirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
            m_CascadeCountId = Shader.PropertyToID("_CascadeCount"),
            m_CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
            m_ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade"),
            m_CascadeDataId = Shader.PropertyToID("_CascadeData"),
            m_ShadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");
        
        private static Matrix4x4[] m_DirShadowMatrices = 
            new Matrix4x4[m_MaxShadowedDirectionalLightCount * m_MaxCascades];

        private static Vector4[] m_CascadeCullingSpheres = new Vector4[m_MaxCascades];

        private static Vector4[] m_CascadeData = new Vector4[m_MaxCascades];

        private static string[] m_DirectionalFilterKeywords = {
            "_DIRECTIONAL_PCF3",
            "_DIRECTIONAL_PCF5",
            "_DIRECTIONAL_PCF7",
        };
        private static string[] m_CascadeBlendKeywords = {
            "_CASCADE_BLEND_SOFT",
            "_CASCADE_BLEND_DITHER"
        };

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

        public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
        {
            // 当该光源不需要阴影 或 阴影强度为0 或 光源超出范围时不生成阴影
            if (m_ShadowedDirectionalLightCount < m_MaxShadowedDirectionalLightCount &&
                light.shadows != LightShadows.None && light.shadowStrength > 0 &&
                m_CullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b)) {
                m_ShadowedDirectionalLights[m_ShadowedDirectionalLightCount] =
                    new ShadowedDirectionalLight {
                        m_VisibleLightIndex = visibleLightIndex,
                        m_SlopeScaleBias = light.shadowBias,
                        m_NearPlaneOffset = light.shadowNearPlane
                    };
                return new Vector3(light.shadowStrength, 
                    m_ShadowSetting.m_Directional.m_CascadeCount * m_ShadowedDirectionalLightCount++,
                    light.shadowNormalBias);
            }

            return Vector3.zero;
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
            int tiles = m_ShadowedDirectionalLightCount * m_ShadowSetting.m_Directional.m_CascadeCount;
            int atlasSize = (int)m_ShadowSetting.m_Directional.m_AtlasSize;
            // 将ShadowMap拆分为多个,每个光源占视口的一部分
            int split = tiles <= 1 ? 1 : (tiles <= 4 ? 2 : 4);
            int tileSize = atlasSize / split;
            
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
                RenderDirectionalShadows(i, split, tileSize);
            }
 
            // 提前进行除法提高效率
            float cascadeFade = 1 - m_ShadowSetting.m_Directional.m_CascadeFade;
            m_CommandBuffer.SetGlobalVector(m_ShadowDistanceFadeId, 
                new Vector4(1.0f / m_ShadowSetting.m_MaxDistance, 
                    1.0f / m_ShadowSetting.m_DistanceFade,
                    1.0f / (1- cascadeFade * cascadeFade)));
            m_CommandBuffer.SetGlobalInt(m_CascadeCountId, m_ShadowSetting.m_Directional.m_CascadeCount);
            m_CommandBuffer.SetGlobalVectorArray(m_CascadeDataId, m_CascadeData);
            m_CommandBuffer.SetGlobalVectorArray(m_CascadeCullingSpheresId, m_CascadeCullingSpheres);
            m_CommandBuffer.SetGlobalMatrixArray(m_DirShadowMatricesId, m_DirShadowMatrices);
            m_CommandBuffer.SetGlobalVector(m_ShadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
            
            SetKeywords(m_DirectionalFilterKeywords, (int)m_ShadowSetting.m_Directional.m_FilterMode - 1);
            SetKeywords(m_CascadeBlendKeywords, (int)m_ShadowSetting.m_Directional.m_CascadeBlendMode - 1);
            
            m_CommandBuffer.EndSample(m_BufferName);
            ExecuteBuffer();
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
        
        // 渲染ShadowMap
        private void RenderDirectionalShadows(int index, int split, int tileSize)
        {
            ShadowedDirectionalLight light = m_ShadowedDirectionalLights[index];
            ShadowDrawingSettings shadowDrawingSettings = new ShadowDrawingSettings(
                m_CullingResults, light.m_VisibleLightIndex);
            
            // 设置视图和投影矩阵
            int cascadeCount = m_ShadowSetting.m_Directional.m_CascadeCount;
            int tileOffset = index * cascadeCount;
            Vector3 ratios = m_ShadowSetting.m_Directional.m_CascadeRatios;
            
            float cullingFactor = Mathf.Max(0f, 0.8f - m_ShadowSetting.m_Directional.m_CascadeFade);
            
            for (int i = 0; i < cascadeCount; i++) {
                m_CullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(
                    light.m_VisibleLightIndex, i, cascadeCount, ratios, tileSize, light.m_NearPlaneOffset,
                    out Matrix4x4 viewMatrix, out Matrix4x4 projectionMatrix,
                    out ShadowSplitData splitData);
                splitData.shadowCascadeBlendCullingFactor = cullingFactor;
                // 阴影分割的剔除信息
                shadowDrawingSettings.splitData = splitData;
                if (index == 0) {   // 获取级联的剔除球体
                    SetCascadeData(i, splitData.cullingSphere, tileSize);
                }
                int tileIndex = tileOffset + i;
                Vector2 offset = SetTileViewport(tileIndex, split, tileSize);
                var viewProj = projectionMatrix * viewMatrix;
                m_DirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(viewProj, offset, split);
                m_CommandBuffer.SetViewProjectionMatrices(viewMatrix, projectionMatrix);
                
                // 设定深度偏差
                m_CommandBuffer.SetGlobalDepthBias(0, light.m_SlopeScaleBias);
                ExecuteBuffer();
                m_RenderContext.DrawShadows(ref shadowDrawingSettings);
                m_CommandBuffer.SetGlobalDepthBias(0, 0);
            }
        }

        private void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
        {
            // 计算阴影贴图纹素的大小,同时考虑斜对角的情况
            float texelSize = 2 * cullingSphere.w / tileSize * 1.4142136f;
            // 由于进行PCF会导致阴影暗疮加剧，因此适当提高Bias
            float filterSize = texelSize * ((float)m_ShadowSetting.m_Directional.m_FilterMode + 1f);
            // 对于剔除球边缘的阴影，不需要采样球外的样本点，通过减小半径来提前结束采样
            cullingSphere.w -= filterSize;
            // 计算剔除球的平方半径，用于在着色器中判度面片是否在球中
            cullingSphere.w *= cullingSphere.w;
            m_CascadeData[index] = new Vector4(1 / cullingSphere.w, filterSize, 0.0f);
            m_CascadeCullingSpheres[index] = cullingSphere;
        }

        /// <summary>
        /// 设置 PCF 的关键字
        /// </summary>
        private void SetKeywords(string[] keywords, int enableIndex)
        {
            for (int i = 0; i < keywords.Length; ++i) {
                if (i == enableIndex) {
                    m_CommandBuffer.EnableShaderKeyword(keywords[i]);
                }
                else {
                    m_CommandBuffer.DisableShaderKeyword(keywords[i]);
                }
            }
        }

        private Vector2 SetTileViewport(int index, int split, int tileSize)
        {
            Vector2 offset = new Vector2(index % split, index / split);
            Rect viewportRect = new Rect(tileSize * offset.x, tileSize * offset.y, tileSize, tileSize);
            m_CommandBuffer.SetViewport(viewportRect);
            return offset;
        }
        
        private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 matrix, Vector2 offset, int split)
        {
            // Unity的矩阵是列矩阵
            if (SystemInfo.usesReversedZBuffer) {
                matrix.m20 = -matrix.m20;
                matrix.m21 = -matrix.m21;
                matrix.m22 = -matrix.m22;
                matrix.m23 = -matrix.m23;
            }
            float scale = 1f / split;
            matrix.m00 = (0.5f * (matrix.m00 + matrix.m30) + offset.x * matrix.m30) * scale;
            matrix.m01 = (0.5f * (matrix.m01 + matrix.m31) + offset.x * matrix.m31) * scale;
            matrix.m02 = (0.5f * (matrix.m02 + matrix.m32) + offset.x * matrix.m32) * scale;
            matrix.m03 = (0.5f * (matrix.m03 + matrix.m33) + offset.x * matrix.m33) * scale;
            matrix.m10 = (0.5f * (matrix.m10 + matrix.m30) + offset.y * matrix.m30) * scale;
            matrix.m11 = (0.5f * (matrix.m11 + matrix.m31) + offset.y * matrix.m31) * scale;
            matrix.m12 = (0.5f * (matrix.m12 + matrix.m32) + offset.y * matrix.m32) * scale;
            matrix.m13 = (0.5f * (matrix.m13 + matrix.m33) + offset.y * matrix.m33) * scale;
            matrix.m20 = 0.5f * (matrix.m20 + matrix.m30);
            matrix.m21 = 0.5f * (matrix.m21 + matrix.m31);
            matrix.m22 = 0.5f * (matrix.m22 + matrix.m32);
            matrix.m23 = 0.5f * (matrix.m23 + matrix.m33);

            return matrix;
        }
    }
}