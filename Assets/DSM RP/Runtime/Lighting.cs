using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    public class Lighting
    {
        private const string m_BufferName = "Lighting";
        private CommandBuffer m_CommandBuffer = new CommandBuffer(){ name = m_BufferName };
        private CullingResults m_CullingResults;
        private const int m_MaxDirLightCount = 4;
        
        Shadows m_Shadows = new Shadows();

        static private int
            m_DirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            m_DirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
            m_DirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
            m_DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
        static private Vector4[]
            m_DirLightColors = new Vector4[m_MaxDirLightCount],
            m_DirLightDirections = new Vector4[m_MaxDirLightCount],
            m_DirLightShadowData = new Vector4[m_MaxDirLightCount];

        public void Setup(
            ScriptableRenderContext context, 
            CullingResults cullingResults,
            ShadowSetting shadowSetting)
        {
            m_CullingResults = cullingResults;
            
            m_CommandBuffer.BeginSample(m_BufferName);
            
            m_Shadows.Setup(context, cullingResults, shadowSetting);
            SetupLights();
            m_Shadows.Render();
            
            m_CommandBuffer.EndSample(m_BufferName);
            context.ExecuteCommandBuffer(m_CommandBuffer);
            m_CommandBuffer.Clear();
        }

        public void CleanUp()
        {
            m_Shadows.CleanUp();
        }

        private void SetupLights()
        {
            NativeArray<VisibleLight> lights = m_CullingResults.visibleLights;
            int maxLightCount = Mathf.Min(lights.Length, m_MaxDirLightCount);
            for (int i = 0; i < maxLightCount; ++i) {
                VisibleLight light = lights[i];
                if (lights[i].lightType == LightType.Directional) {
                    SetupDirectionalLight(i, ref light);
                }
            };
            m_CommandBuffer.SetGlobalInt(m_DirLightCountId, maxLightCount);
            m_CommandBuffer.SetGlobalVectorArray(m_DirLightColorsId, m_DirLightColors);
            m_CommandBuffer.SetGlobalVectorArray(m_DirLightDirectionsId, m_DirLightDirections);
            m_CommandBuffer.SetGlobalVectorArray(m_DirLightShadowDataId, m_DirLightShadowData);
        }

        private void SetupDirectionalLight(int index, ref VisibleLight light)
        {
            m_DirLightColors[index] = light.finalColor;
            m_DirLightDirections[index] = -light.localToWorldMatrix.GetColumn(2);
            m_DirLightShadowData[index] = m_Shadows.ReserveDirectionalShadows(light.light, index);
        }
    }
}