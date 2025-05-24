using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM
{
    public partial class CameraRender
    {   
        private ScriptableRenderContext m_RenderContext;    // 类似命令队列
        private Camera m_RenderCamera;

        private const string m_BufferName = "RenderCamera";
        private CommandBuffer m_CommandBuffer = new CommandBuffer { name = m_BufferName };  // 类似命令列表
        
        private CullingResults m_CullingResults;
        
        private Lighting m_Light = new Lighting();
        
        private PostEffectManager m_PostEffectManager;
        
        private RenderTexture m_NormalTexture;
        
        static private ShaderTagId 
            m_UnlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
            m_LitShaderTagId = new ShaderTagId("DSMLit");

        public static readonly int
            m_CameraColorTextureId = Shader.PropertyToID("_CameraColorTexture"),
            m_CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture"),
            m_NormalTextureId = Shader.PropertyToID("_NormalTexture");
        
        public void Render(
            ScriptableRenderContext context, 
            Camera camera, 
            bool useDynamicBatching, 
            bool useGPUInstancing,
            ShadowSetting shadowSetting, 
            PostEffectManager postEffectManager)
        {
            if (context == null || camera == null) {
                Debug.LogError("Context or Camera should no be null");
                return;
            }
            
            m_RenderContext = context;
            m_RenderCamera = camera;
            m_PostEffectManager = postEffectManager;
            
            // 可能会增加场景物体，在剔除前进行
            PrepareBuffer();
            PrepareForSceneWindow();
            if (!Cull(shadowSetting.m_MaxDistance)) return;
            
            Debug.Log("Render Camera");

            m_CommandBuffer.BeginSample(m_SampleName);
            ExecuteBuffer();
            
            m_Light.Setup(context, m_CullingResults, shadowSetting);
            m_PostEffectManager.Setup(context, m_RenderCamera, m_CullingResults);
            
            m_CommandBuffer.EndSample(m_SampleName);
            Setup();
            
            DrawVisibleGeometry(useDynamicBatching, useGPUInstancing);
            DrawUnsupportedShaders();
            if (m_PostEffectManager.IsActive) {   // 进行屏幕后处理
                m_CommandBuffer.SetGlobalTexture(m_NormalTextureId, m_NormalTexture);
                m_PostEffectManager.Render(m_CameraColorTextureId);
            }
            
            DrawGizmos();

            Cleanup();
            
            Submit();
        }
        
        /// <summary>
        /// 获取剔除信息
        /// </summary>
        /// <returns></returns>
        private bool Cull(float maxShadowDistance)
        {
            if (m_RenderCamera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters)) {
                cullingParameters.shadowDistance = Mathf.Min(m_RenderCamera.farClipPlane, maxShadowDistance);
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
            flags = flags > CameraClearFlags.Color ? CameraClearFlags.Color : flags;    // 确保临时纹理被清理
            // 创建临时纹理并渲染到纹理上
            m_CommandBuffer.GetTemporaryRT(
                m_CameraColorTextureId, m_RenderCamera.pixelWidth, m_RenderCamera.pixelHeight);
            m_CommandBuffer.GetTemporaryRT(
                m_CameraDepthTextureId, m_RenderCamera.pixelWidth, m_RenderCamera.pixelHeight,
                32, FilterMode.Point, RenderTextureFormat.Depth);
            m_CommandBuffer.GetTemporaryRT(
                m_NormalTextureId, m_RenderCamera.pixelWidth, m_RenderCamera.pixelHeight, 
                0, FilterMode.Point, RenderTextureFormat.RGB111110Float);
            
            RenderTargetIdentifier[] renderTargets = {
                m_CameraColorTextureId,
                m_NormalTextureId
            };
            
            m_CommandBuffer.SetRenderTarget(renderTargets, m_CameraDepthTextureId);

            RTClearFlags clearFlags = RTClearFlags.All;
            if (flags == CameraClearFlags.Nothing) {
                clearFlags = RTClearFlags.None;
            }
            else if (flags == CameraClearFlags.Depth) {
                clearFlags = RTClearFlags.DepthStencil;
            }
            Color[] clearColors = {
                flags == CameraClearFlags.Color ? 
                    m_RenderCamera.backgroundColor.linear : Color.clear,
                Color.clear
            };
            m_CommandBuffer.ClearRenderTarget(clearFlags, clearColors);
            
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
                enableDynamicBatching = useDynamicBatching,
                perObjectData = PerObjectData.Lightmaps | 
                                PerObjectData.LightProbe | 
                                PerObjectData.LightProbeProxyVolume
            };
            
            drawingSettings.SetShaderPassName(1, m_LitShaderTagId);
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

        private void Cleanup()
        {
            m_Light.Cleanup();
            if (m_PostEffectManager.IsActive) {   // 释放临时纹理
                m_CommandBuffer.ReleaseTemporaryRT(m_CameraColorTextureId);
                m_CommandBuffer.ReleaseTemporaryRT(m_CameraDepthTextureId);
            }
            RenderTexture.ReleaseTemporary(m_NormalTexture);
        }

    }
}
