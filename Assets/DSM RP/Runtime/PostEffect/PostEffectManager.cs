using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DSM
{
    [Serializable] 
    public class PostEffectManager
    {
        public class PostEffect : ScriptableObject, IComparable<PostEffect>
        {
            private int m_Weight = 0;
            protected Material m_Material;
        
            public Material Material { get { return m_Material; } }

            virtual public void Render(
                CommandBuffer cmd, 
                RenderTargetIdentifier src,
                RenderTargetIdentifier dest,
                Camera camera)
            {
                cmd.Blit(src, dest);
            }

            public int CompareTo(PostEffect other)
            {
                if (ReferenceEquals(this, other)) return 0;
                if (other is null) return 1;
                return m_Weight.CompareTo(other.m_Weight);
            }
        }
            
        private const string m_BufferName = "PostEffects";
        
        private ScriptableRenderContext m_RenderContext;
        private Camera m_RenderCamera;
        
        [SerializeField] private List<PostEffect> m_PostEffects = new List<PostEffect>();

        public List<PostEffect> PostEffects { get { return m_PostEffects; } }
        
        public bool IsActive => m_PostEffects != null;
        

        public void Setup(
            ScriptableRenderContext renderContext,
            Camera camera,
            CullingResults cullingResults)
        {
            m_RenderContext = renderContext;
            m_RenderCamera = camera;
        }

        public void Render(
            CommandBuffer cmd,
            RenderTargetIdentifier src,
            RenderTargetIdentifier dest,
            PostEffect postEffect,
            Camera camera)
        {
            postEffect.Render(cmd, src, dest, camera);
            m_RenderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }

        /// <summary>
        /// 执行屏幕后处理
        /// </summary>
        /// <param name="sourceId"></param> 执行后处理操作的缓冲区的ID
        public void Render(int sourceId)
        {
            Debug.Log("Render PostEffect");

            CommandBuffer cmd = CommandBufferPool.Get(m_BufferName);
            
            RenderTargetIdentifier[] rtIdentifier = new RenderTargetIdentifier[2];
            rtIdentifier[0] = sourceId;
            rtIdentifier[1] = BuiltinRenderTextureType.CameraTarget;
            m_PostEffects.Sort(); // 根据后处理的权重进行排序
            
            for (int i = 0; i < m_PostEffects.Count; i++) {
                if (m_PostEffects[i] != null) {
                    Render(cmd, rtIdentifier[i % 2],
                        rtIdentifier[(i + 1) % 2],
                        m_PostEffects[i],
                        m_RenderCamera);
                }
            }
            
            // 偶数次的话手动拷贝到相机当中
            if (m_PostEffects.Count % 2 == 0) {
                cmd.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
                m_RenderContext.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }
            
            CommandBufferPool.Release(cmd);
        }
    }
}