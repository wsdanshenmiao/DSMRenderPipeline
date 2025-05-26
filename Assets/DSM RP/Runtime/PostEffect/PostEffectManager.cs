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
        public class PostEffect : ScriptableObject, IComparable<PostEffect>
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

            virtual public void Render(RenderGraphContext context) { }

            virtual public void Record(
                RenderGraph renderGraph,
                CullingResults cullingResults,
                Camera camera,
                ScriptableRenderContext renderContext,
                in CameraRendererTextures cameraTextures) { }
        }
        
        [SerializeField] private List<PostEffect> sm_PostEffects = new();
        
        public bool IsActive => sm_PostEffects != null;
        


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

            RenderTargetIdentifier[] rtIdentifier = new RenderTargetIdentifier[2];
            rtIdentifier[0] = cameraTextures.m_ColorTexture;
            rtIdentifier[1] = BuiltinRenderTextureType.CameraTarget;
            sm_PostEffects.Sort(); // 根据后处理的权重进行排序
            
            foreach(PostEffect postEffect in sm_PostEffects)
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