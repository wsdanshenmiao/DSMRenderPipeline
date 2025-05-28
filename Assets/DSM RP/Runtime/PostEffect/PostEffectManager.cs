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
        public abstract class PostEffectSetting : ScriptableObject, IComparable<PostEffectSetting>
        {
            public int m_Weight = 0;
            
            public int CompareTo(PostEffectSetting other)
            {
                if (ReferenceEquals(this, other)) return 0;
                if (other is null) return 1;
                return m_Weight.CompareTo(other.m_Weight);
            }

            public abstract PostEffect GetPostEffect();
        }
            
        public abstract class PostEffect
        {
            protected Material m_Material;
        
            public Material Material { get { return m_Material; } }

            abstract protected void Render(RenderGraphContext context);

            abstract public void Record(
                RenderGraph renderGraph,
                CullingResults cullingResults,
                Camera camera,
                ScriptableRenderContext renderContext,
                in CameraRendererTextures cameraTextures);
        }
        
        private static readonly ProfilingSampler sm_Sampler = new ProfilingSampler("PostEffect");
        
        [SerializeField] private List<PostEffectSetting> m_PostEffects = new();
        
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
            
            foreach(PostEffectSetting setting in m_PostEffects)
            {
                if(setting == null) continue;
                
                setting.GetPostEffect().Record(
                    renderGraph,
                    cullingResults, 
                    camera, 
                    renderContext,
                    cameraTextures);
            }
        }
    }
}