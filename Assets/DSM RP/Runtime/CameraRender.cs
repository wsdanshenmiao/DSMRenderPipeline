using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    public partial class CameraRender
    {
        private ScriptableRenderContext m_RenderContext;    // 类似命令队列
        private Camera m_RenderCamera;

        private const string m_BufferName = "RenderCamera";
        private CommandBuffer m_CommandBuffer = new CommandBuffer { name = m_BufferName };  // 类似命令列表
        
        private CullingResults m_CullingResults;
        
        static private ShaderTagId m_UnlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        
        public void Render(
            ScriptableRenderContext context, 
            Camera camera, 
            bool useDynamicBatching, 
            bool useGPUInstancing)
        {
            if (context == null || camera == null) {
                Debug.LogError("Context or Camera should no be null");
                return;
            }
            
            m_RenderContext = context;
            m_RenderCamera = camera;

            // 可能会增加场景物体，在剔除前进行
            PrepareBuffer();
            PrepareForSceneWindow();
            if (!Cull()) return;
            
            Debug.Log("Render Camera");

            Setup();
            
            DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
            DrawUnsupportedShaders();
            DrawGizmos();

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
            CameraClearFlags flags = m_RenderCamera.clearFlags;
            m_CommandBuffer.ClearRenderTarget(
                flags <= CameraClearFlags.Depth, 
                flags == CameraClearFlags.Color, 
                flags == CameraClearFlags.Color ? m_RenderCamera.backgroundColor.linear : Color.clear);
            // 加入分析器样本，方便调试的时候定位
            m_CommandBuffer.BeginSample(m_SampleName);
            ExecuteBuffer();
        }
        
        // 绘制可见的几何体，可选择优化绘制策略
        private void DrawVisibleGeometry(bool useDynamicBatching, bool useGPUInstancing)
        {
            //Debug.Log("Draw Visible Geometry");
            
            // 绘制物体
            
            SortingSettings sortingSettings = new SortingSettings(m_RenderCamera) {
                criteria = SortingCriteria.CommonOpaque // 按不透明体的顺序排序
            };
            DrawingSettings drawingSettings = new DrawingSettings(m_UnlitShaderTagId, sortingSettings)
            {
                enableInstancing = useGPUInstancing,
                enableDynamicBatching = useDynamicBatching
            };
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
        /// 提交渲染命令
        /// </summary>
        private void Submit()
        {
            m_CommandBuffer.EndSample(m_SampleName);
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
