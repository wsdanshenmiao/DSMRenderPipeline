using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace DSM
{
    [Serializable] 
    public class PostEffectManager
    {
        public abstract class PostEffect : ScriptableObject, IComparable<PostEffect>
        {
            private int m_Weight = 0;
            protected Material m_Material;
        
            public Material Material { get { return m_Material; } }

            public int CompareTo(PostEffect other)
            {
                if (ReferenceEquals(this, other)) return 0;
                if (other is null) return 1;
                return m_Weight.CompareTo(other.m_Weight);
            }

            abstract protected void Render(RenderGraphContext context);

            abstract public void Record(
                RenderGraph renderGraph,
                CullingResults cullingResults,
                Camera camera,
                ScriptableRenderContext renderContext,
                in CameraRendererTextures cameraTextures);
        }
        
        private static readonly ProfilingSampler sm_Sampler = new ProfilingSampler("PostEffect");
        
        [SerializeField] private List<PostEffect> m_PostEffects = new();
        
        public bool IsActive => m_PostEffects != null && m_PostEffects.Count > 0;
        


        /// <summary>
        /// 执行屏幕后处理
        /// </summary>
        public void Record(
            RenderGraph renderGraph,
            CullingResults cullingResults,
            Camera camera,
            ScriptableRenderContext renderContext,
            in CameraRendererTextures cameraTextures)
        {
            //Debug.Log("Render PostEffect");
            if(!IsActive) return;
            //Debug.Log("Active");

            using var groupSampler = new RenderGraphProfilingScope(renderGraph, sm_Sampler);
            
            m_PostEffects.Sort(); // 根据后处理的权重进行排序
            
            foreach(PostEffect postEffect in m_PostEffects)
            {
                if(postEffect == null) continue;
                
                postEffect.Record(
                    renderGraph,
                    cullingResults, 
                    camera, 
                    renderContext,
                    cameraTextures);
            }
        }
    }
}