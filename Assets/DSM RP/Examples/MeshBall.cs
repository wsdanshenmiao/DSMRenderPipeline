using UnityEngine;

namespace DSM
{
    public class MeshBall : MonoBehaviour
    {
        private const int m_MaxCount = 1023;
        
        static private int m_BaseColorID = Shader.PropertyToID("_BaseColor"),
            m_MetallicId = Shader.PropertyToID("_Metallic"),
            m_SmoothnessId = Shader.PropertyToID("_Smoothness");
        
        float[] m_Metallic = new float[m_MaxCount],
            m_Smoothness = new float[m_MaxCount];
        
        [SerializeField]
        private Mesh m_Mesh = default;
        
        [SerializeField]
        private Material m_Material = default;

        private Matrix4x4[] m_Matrices = new Matrix4x4[m_MaxCount];
        private Vector4[] m_Colors = new Vector4[m_MaxCount];
        
        private MaterialPropertyBlock m_PropertyBlock;
        
        private void Awake()
        {
            for (int i = 0; i < m_Matrices.Length;++i) {
                m_Matrices[i] = Matrix4x4.TRS(
                    Random.insideUnitSphere * 10, 
                    Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f), 
                    Vector3.one * Random.Range(0.5f, 1.5f));
                m_Colors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f));
                m_Metallic[i] = Random.value < 0.25f ? 1f : 0f;
                m_Smoothness[i] = Random.Range(0.05f, 0.95f);
            }
        }

        private void Update()
        {
            if (m_PropertyBlock == null) {
                m_PropertyBlock = new MaterialPropertyBlock();
                m_PropertyBlock.SetFloatArray(m_MetallicId, m_Metallic);
                m_PropertyBlock.SetFloatArray(m_SmoothnessId, m_Smoothness);
                m_PropertyBlock.SetVectorArray(m_BaseColorID, m_Colors);
            }
            Graphics.DrawMeshInstanced(m_Mesh, 0, m_Material, m_Matrices, m_Matrices.Length, m_PropertyBlock);
        }
    }
}