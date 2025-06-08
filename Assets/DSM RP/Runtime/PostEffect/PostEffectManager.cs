using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Serialization;

namespace DSM
{
    public abstract class PostEffectSetting : ScriptableObject, IComparable<PostEffectSetting>
     {
         public int m_Weight = 0;
         public bool m_Enable = true;
         
         public int CompareTo(PostEffectSetting other)
         {
             if (ReferenceEquals(this, other)) return 0;
             if (other is null) return 1;
             return m_Weight.CompareTo(other.m_Weight);
         }

        public abstract void Record(
             RenderGraph renderGraph,
             CullingResults cullingResults,
             Camera camera,
             in CameraRendererTextures cameraTextures,
             TextureHandle target);
    }
         
     public abstract class PostEffect
     {
         protected Material m_Material;

         public abstract void Render(RenderGraphContext context);
     }
    
    [Serializable] 
    public class PostEffectManager
    {
        private static readonly ProfilingSampler sm_Sampler = new ProfilingSampler("PostEffect");
        
        [FormerlySerializedAs("m_PostEffects")] [SerializeField] private List<PostEffectSetting> m_PostEffectSettings = new();
        [SerializeField] private bool m_Enabled = true;
        
        public bool IsActive => m_PostEffectSettings != null && m_PostEffectSettings.Count > 0 && m_Enabled;


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
            if(!IsActive) return;
            using var groupSampler = new RenderGraphProfilingScope(renderGraph, sm_Sampler);

            m_PostEffectSettings.Sort(); // 根据后处理的权重进行排序
            
            for(int i = 0; i < m_PostEffectSettings.Count; i++)
            {
                if(m_PostEffectSettings[i] == null || !m_PostEffectSettings[i].m_Enable) continue;

                m_PostEffectSettings[i].Record(
                    renderGraph,
                    cullingResults, 
                    camera, 
                    cameraTextures,
                    cameraTextures.m_ColorTexture);
            }

        }
    }
}