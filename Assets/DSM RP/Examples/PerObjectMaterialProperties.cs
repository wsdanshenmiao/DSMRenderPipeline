using System;
using UnityEngine;


namespace DSM
{
    [DisallowMultipleComponent]
    public class PerObjectMaterialProperties : MonoBehaviour
    {
        static private int m_BaseColorID = Shader.PropertyToID("_BaseColor");
        static private int m_CutoffID = Shader.PropertyToID("_Cutoff");
        
        [SerializeField]
        private Color m_BaseColor = Color.white;
        [SerializeField, Range(0, 1)]
        private float m_Cutoff = 0.5f;

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
            m_MaterialPropertyBlock.SetColor(m_BaseColorID, m_BaseColor);
            m_MaterialPropertyBlock.SetFloat(m_CutoffID, m_Cutoff);
            GetComponent<Renderer>().SetPropertyBlock(m_MaterialPropertyBlock);
        }
    }
}