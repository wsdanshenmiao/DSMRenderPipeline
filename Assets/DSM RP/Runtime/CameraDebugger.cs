using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;


namespace DSM{

    public static class CameraDebugger
    {
        const string m_PanelName = "Forward+";
        static readonly int m_DebugAlphaId = Shader.PropertyToID("_DebugAlpha");
        
        static Material m_Material;

        static bool m_ShowTiles;
        static float m_Alpha = 0.5f;

        public static bool IsActive => m_ShowTiles && m_Alpha > 0;

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        static public void Initialize(Shader debugShader)
        {
            if(debugShader == null)
            {
                return;
            }
            m_Material = CoreUtils.CreateEngineMaterial(debugShader);
            DebugManager.instance.GetPanel(m_PanelName, true).children.Add(
                new DebugUI.FloatField
                {
                    displayName = "Alpha",
                    tooltip = "Alpha value of the debug overlay.",
                    min = static () => 0.0f,
                    max = static () => 1.0f,
                    getter = () => m_Alpha,
                    setter = value => m_Alpha = Mathf.Clamp01(value)
                },
                new DebugUI.BoolField
                {
                    displayName = "Show Tiles",
                    tooltip = "Whether the debug overlay is shown.",
                    getter = () => m_ShowTiles,
                    setter = value => m_ShowTiles = value
                });
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        static public void Cleanup()
        {
            CoreUtils.Destroy(m_Material);
            DebugManager.instance.RemovePanel(m_PanelName);
            m_Material = null;
        }
        
        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        static public void Render(RenderGraphContext context)
        {
            CommandBuffer cmd = context.cmd;
            cmd.SetGlobalFloat(m_DebugAlphaId, m_Alpha);
            cmd.DrawProcedural(Matrix4x4.identity, m_Material, 0, MeshTopology.Triangles, 3);
            context.renderContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }
    }
}