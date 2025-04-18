using System;
using UnityEngine;


namespace DSM
{
    [DisallowMultipleComponent]
    public class PerObjectMaterialProperties : MonoBehaviour
    {
        static private int m_BaseColorID = Shader.PropertyToID("_BaseColor"),
            m_CutoffID = Shader.PropertyToID("_Cutoff"),
            m_MetallicID = Shader.PropertyToID("_Metallic"),
            m_SmoothnessID = Shader.PropertyToID("_Smoothness"),
            m_EmissionColorID = Shader.PropertyToID("_EmissionColor");
        
        
        [SerializeField]
        private Color m_BaseColor = Color.white;
        [SerializeField, Range(0, 1)]
        private float m_Cutoff = 0.5f, m_Metallic = 0, m_Smoothness = 0.5f;
        [SerializeField, ColorUsage(false, true)]
        Color m_EmissionColor = Color.black;
        
        private MaterialPropertyBlock m_MaterialPropertyBlock;
        
        private void Awake()
        {
            OnValidate();
        }

        private void OnValidate()
        {
            if (m_MaterialPropertyBlock == null) {
                m_MaterialPropertyBlock = new MaterialPropertyBlock();
            }
            
            m_MaterialPropertyBlock.SetColor(m_EmissionColorID, m_EmissionColor);
            m_MaterialPropertyBlock.SetFloat(m_MetallicID, m_Metallic);
            m_MaterialPropertyBlock.SetFloat(m_SmoothnessID, m_Smoothness);
            m_MaterialPropertyBlock.SetColor(m_BaseColorID, m_BaseColor);
            m_MaterialPropertyBlock.SetFloat(m_CutoffID, m_Cutoff);
            GetComponent<Renderer>().SetPropertyBlock(m_MaterialPropertyBlock);
        }
    }
}