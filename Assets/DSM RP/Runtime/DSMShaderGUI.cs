using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class DSMShaderGUI : ShaderGUI
{
        private MaterialEditor m_MaterialEditor;
        private MaterialProperty[] m_MaterialProperties;
        private Object[] m_Materials;
        
        bool showPresets;
        
        bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");
        bool HasProperty (string name) =>
                FindProperty(name, m_MaterialProperties, false) != null;
        bool Clipping {
                set => SetProperty("_Clipping", "_CLIPPING", value);
        }
        bool PremultiplyAlpha {
                set => SetProperty("_PremulAlpha", "_PREMULTIPLY_ALPHA", value);
        }
        BlendMode SrcBlend {
                set => SetProperty("_SrcBlend", (float)value);
        }
        BlendMode DstBlend {
                set => SetProperty("_DstBlend", (float)value);
        }
        bool ZWrite {
                set => SetProperty("_ZWrite", value ? 1f : 0f);
        }
        RenderQueue RenderQueue {
                set {
                        foreach (Material m in m_Materials) {
                                m.renderQueue = (int)value;
                        }
                }
        }
        
        public override void OnGUI (MaterialEditor materialEditor, MaterialProperty[] properties) {
                base.OnGUI(materialEditor, properties);
                m_MaterialEditor = materialEditor;
                m_Materials = materialEditor.targets;
                this.m_MaterialProperties = properties;

                EditorGUILayout.Space();
                showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
                if (showPresets) {
                        OpaquePreset();
                        ClipPreset();
                        FadePreset();
                        TransparentPreset();
                }
        }

        private void SetProperty(string name , float value)
        {
                FindProperty(name, m_MaterialProperties).floatValue = value;
        }

        private void SetKeyword(string name, bool enabled)
        { 
                if (enabled) {
                        foreach (Material material in m_Materials) {
                                material.EnableKeyword(name);
                        }
                }
                else {
                        foreach (Material material in m_Materials) {
                                material.DisableKeyword(name);
                        } 
                }
        }
        
        void SetProperty (string name, string keyword, bool value) {
                SetProperty(name, value ? 1f : 0f);
                SetKeyword(keyword, value);
        }

        void OpaquePreset () {
                if (PresetButton("Opaque")) {
                        Clipping = false;
                        PremultiplyAlpha = false;
                        SrcBlend = BlendMode.One;
                        DstBlend = BlendMode.Zero;
                        ZWrite = true;
                        RenderQueue = RenderQueue.Geometry;
                }
        }

        void ClipPreset () {
                if (PresetButton("Clip")) {
                        Clipping = true;
                        PremultiplyAlpha = false;
                        SrcBlend = BlendMode.One;
                        DstBlend = BlendMode.Zero;
                        ZWrite = true;
                        RenderQueue = RenderQueue.AlphaTest;
                }
        }

        void FadePreset () {
                if (PresetButton("Fade")) {
                        Clipping = false;
                        PremultiplyAlpha = false;
                        SrcBlend = BlendMode.SrcAlpha;
                        DstBlend = BlendMode.OneMinusSrcAlpha;
                        ZWrite = false;
                        RenderQueue = RenderQueue.Transparent;
                }
        }

        void TransparentPreset () {
                if (HasPremultiplyAlpha && PresetButton("Transparent")) {
                        Clipping = false;
                        PremultiplyAlpha = true;
                        SrcBlend = BlendMode.One;
                        DstBlend = BlendMode.OneMinusSrcAlpha;
                        ZWrite = false;
                        RenderQueue = RenderQueue.Transparent;
                }
        }

        bool PresetButton (string name) {
                if (GUILayout.Button(name)) {
                        m_MaterialEditor.RegisterPropertyChangeUndo(name);
                        return true;
                }
                return false;
        }


}