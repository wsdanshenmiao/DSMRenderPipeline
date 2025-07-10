using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM
{
    // 高斯模糊
    public class GaussianBlurPass
    {
        private static ProfilingSampler sm_Sampler = new ProfilingSampler(nameof(GaussianBlurPass));
       
        private TextureHandle m_TargetTexture;
        private TextureHandle m_TmpTexture;
        private TextureHandle m_TmpTargetTexture;

        private BufferHandle m_BlurWeights;

        private int m_TexWidth, m_TexHeight;
        private uint m_BlurRadius;
        
        private ComputeShader m_BlurShader = null;

        public static readonly int sm_ThreadInGroup = 256;
        
        public static readonly int
            m_BlurRadiusId = Shader.PropertyToID("_BlurRadius"),
            m_BlurWeightsId = Shader.PropertyToID("_BlurWeights"),
            m_SrcTextureId = Shader.PropertyToID("_SrcTexture"),
            m_DstTextureId = Shader.PropertyToID("_DstTexture");
        
        public void Render(RenderGraphContext context)
        {
            if (m_BlurRadius < 1) return;
            
            CommandBuffer cmd = context.cmd;

            // 原纹理是否是UAV
            bool useTmpTarget = m_TmpTargetTexture.IsValid();
            TextureHandle targetTex = m_TargetTexture;
            if (useTmpTarget) {   // 不是的话需要
                cmd.CopyTexture(m_TargetTexture, m_TmpTargetTexture);
                targetTex = m_TmpTargetTexture;
            }
            
            int horizKernel = m_BlurShader.FindKernel("HorizBlurCS");
            int verticKernel = m_BlurShader.FindKernel("VerticBlurCS");
            
            float[] blurWeights = CalculateGaussianWeight(m_BlurRadius);
            cmd.SetBufferData(m_BlurWeights, blurWeights);
            
            m_BlurShader.SetBuffer(horizKernel, m_BlurWeightsId, m_BlurWeights);
            m_BlurShader.SetBuffer(verticKernel, m_BlurWeightsId, m_BlurWeights);
            
            m_BlurShader.SetInt(m_BlurRadiusId, (int)m_BlurRadius);
            
            // 横向模糊
            int threadGroupX = (m_TexWidth / sm_ThreadInGroup) + 1;
            int threadGroupY = m_TexHeight;
            m_BlurShader.SetTexture(horizKernel, m_SrcTextureId, targetTex);
            m_BlurShader.SetTexture(horizKernel, m_DstTextureId, m_TmpTexture);
            cmd.DispatchCompute(m_BlurShader, horizKernel, threadGroupX, threadGroupY, 1);

            // 纵向模糊
            threadGroupX = m_TexWidth;
            threadGroupY = (m_TexHeight / sm_ThreadInGroup) + 1;
            m_BlurShader.SetTexture(verticKernel, m_SrcTextureId, m_TmpTexture);
            m_BlurShader.SetTexture(verticKernel, m_DstTextureId, targetTex);
            cmd.DispatchCompute(m_BlurShader, verticKernel, threadGroupX, threadGroupY, 1);

            // 需要拷贝回去
            if (useTmpTarget) {
                cmd.CopyTexture(targetTex, m_TargetTexture);
            }
            
            context.renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        public static void Record(
            RenderGraph renderGraph,
            ComputeShader computeShader,
            TextureHandle target,
            uint blurRadius,
            int width, int height)
        {
            var builder = renderGraph.AddRenderPass(
                sm_Sampler.name, out GaussianBlurPass pass, sm_Sampler);
            
            pass.m_TargetTexture = builder.ReadTexture(target);
            pass.m_TexWidth = width;
            pass.m_TexHeight = height;
            pass.m_BlurRadius = blurRadius;
            pass.m_BlurShader = computeShader;
            
            TextureDesc texDesc = renderGraph.GetTextureDesc(target);
            // 若不是UAV则新建一个临时纹理
            if (!texDesc.enableRandomWrite) {
                texDesc.name = "TmpTargetTexture";
                texDesc.enableRandomWrite = true;
                pass.m_TmpTargetTexture = builder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));
            }

            texDesc.name = "BlurTmpTexture";
            texDesc.enableRandomWrite = true;
            pass.m_TmpTexture = builder.ReadWriteTexture(renderGraph.CreateTexture(texDesc));

            pass.m_BlurWeights = builder.WriteBuffer(renderGraph.CreateBuffer(
                new BufferDesc(2 * (int)blurRadius + 1, sizeof(float))
                {
                    name = "BlurWeights",
                }));

            builder.SetRenderFunc<GaussianBlurPass>(
                static (pass, context) => pass.Render(context));
        }

        private float[] CalculateGaussianWeight(uint blurRadius)
        {
            const float sigma = 2.5f;
            
            float weightSum = 0;
            uint weightSize = 2 * blurRadius + 1;
            float step = (2 * sigma) / (weightSize - 1);
            float[] blurWeights = new float[weightSize];
            
            // 取 2 sigma 范围
            for (int i = 0; i < blurWeights.Length; ++i) {
                float x = i * step - sigma;
                blurWeights[i] = GaussianFunction(x, sigma);
                weightSum += blurWeights[i];
            }

            for (int i = 0; i < blurWeights.Length; ++i) {
                blurWeights[i] /= weightSum;
            }
            return blurWeights;
        }

        private float GaussianFunction(float x, float sigma)
        {
            float sigma2 = sigma * sigma;
            return Mathf.Exp(-x * x / (2 * sigma2)) / Mathf.Sqrt(2 * Mathf.PI * sigma2);
        }
    }
}