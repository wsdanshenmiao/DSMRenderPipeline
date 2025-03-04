using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.Profiling;

namespace DSM
{
    partial class CameraRender
    {
        static ShaderTagId[] m_LegacyShaderTagIds = {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM")
        };

        static private Material m_ErrorMaterial;
        
        partial void DrawUnsupportedShaders();
        partial void DrawGizmos();
        partial void PrepareForSceneWindow();
        partial void PrepareBuffer();
        
#if UNITY_EDITOR

        private string m_SampleName{ get; set; }
        
        
        /// <summary>
        /// 绘制不支持的Shader
        /// </summary>
        partial void DrawUnsupportedShaders()
        {
            //Debug.Log("DrawUnsupportedShaders");
            if (m_ErrorMaterial == null) {
                m_ErrorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
            }
            
            var drawingSettings  = new DrawingSettings(
                m_LegacyShaderTagIds[0], new SortingSettings(m_RenderCamera)) {
                overrideMaterial = m_ErrorMaterial  // 设置不支持的材质样式
            };
            var filteringSettings = FilteringSettings.defaultValue;
            // 设置多个Pass
            for (int i = 1; i < m_LegacyShaderTagIds.Length; ++i) {
                drawingSettings.SetShaderPassName(i, m_LegacyShaderTagIds[i]);
            }
            m_RenderContext.DrawRenderers(m_CullingResults, ref drawingSettings, ref filteringSettings);
        }

        partial void DrawGizmos()
        {
            // 判断是否需要绘制小物件
            if (Handles.ShouldRenderGizmos()) {
                m_RenderContext.DrawGizmos(m_RenderCamera, GizmoSubset.PreImageEffects);
                m_RenderContext.DrawGizmos(m_RenderCamera, GizmoSubset.PostImageEffects);
            }
        }

        partial void PrepareForSceneWindow()
        {
            if (m_RenderCamera.cameraType == CameraType.SceneView) {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(m_RenderCamera);
            }
        }

        partial void PrepareBuffer()
        {
            Profiler.BeginSample("Editor Only");
            m_CommandBuffer.name = m_SampleName = m_RenderCamera.name;
            Profiler.EndSample();
        }

#else
        private string m_SampleName => m_BufferName;

#endif

    }
}