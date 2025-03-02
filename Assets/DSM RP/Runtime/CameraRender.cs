using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    public class CameraRender
    {
        private ScriptableRenderContext m_RenderContext;    // 类似命令队列
        private Camera m_RenderCamera;

        private const string m_BufferName = "RenderCamera";
        private CommandBuffer m_CommandBuffer = new CommandBuffer { name = m_BufferName };  // 类似命令列表
        
        private CullingResults m_CullingResults;
        
        static private ShaderTagId m_UnlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        static ShaderTagId[] m_LegacyShaderTagIds = {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM")
        };

        static private Material m_ErrorMaterial;
        
        public void Render(ScriptableRenderContext context, Camera camera)
        {
            if (context == null || camera == null) {
                Debug.LogError("Context or Camera should no be null");
                return;
            }
            
            m_RenderContext = context;
            m_RenderCamera = camera;

            if (!Cull()) return;
            
            Debug.Log("Render Camera");

            Setup();
            
            DrawVisibleGeometry();
            DrawUnsupportedShaders();

            Submit();
        }
        
        /// <summary>
        /// 获取剔除信息
        /// </summary>
        /// <returns></returns>
        private bool Cull()
        {
            if (m_RenderCamera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters)) {
                m_CullingResults = m_RenderContext.Cull(ref cullingParameters);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 将相机的变换矩阵等属性应用到上下文
        /// </summary>
        private void Setup()
        {
            // 提前设置相机的属性
            m_RenderContext.SetupCameraProperties(m_RenderCamera);
            m_CommandBuffer.ClearRenderTarget(true, true, Color.clear, 1.0f);
            // 加入分析器样本，方便调试的时候定位
            m_CommandBuffer.BeginSample(m_BufferName);
            ExecuteBuffer();
        }
        
        // 绘制天空盒
        private void DrawVisibleGeometry()
        {
            //Debug.Log("Draw Visible Geometry");
            
            // 绘制物体
            
            SortingSettings sortingSettings = new SortingSettings(m_RenderCamera) {
                criteria = SortingCriteria.CommonOpaque // 按不透明体的顺序排序
            };
            DrawingSettings drawingSettings = new DrawingSettings(m_UnlitShaderTagId, sortingSettings);
            // 先绘制不透明体
            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            m_RenderContext.DrawRenderers(m_CullingResults, ref drawingSettings, ref filteringSettings);
            
            m_RenderContext.DrawSkybox(m_RenderCamera);
            
            // 为防止被天空盒遮挡，天空盒后绘制透明体
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;
            m_RenderContext.DrawRenderers(m_CullingResults, ref drawingSettings, ref filteringSettings);
        }

        /// <summary>
        /// 绘制不支持的Shader
        /// </summary>
        private void DrawUnsupportedShaders()
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
        
        /// <summary>
        /// 提交渲染命令
        /// </summary>
        private void Submit()
        {
            m_CommandBuffer.EndSample(m_BufferName);
            ExecuteBuffer();
            m_RenderContext.Submit();
        }

        /// <summary>
        /// 提交命令列表，并清除
        /// </summary>
        private void ExecuteBuffer()
        {
            m_RenderContext.ExecuteCommandBuffer(m_CommandBuffer);
            m_CommandBuffer.Clear();
        }

    }
}
