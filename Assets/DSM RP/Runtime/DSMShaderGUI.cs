using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class DSMShaderGUI : ShaderGUI
{
        enum ShadowMode {
                On, Clip, Dither, Off
        }
        
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
        
        ShadowMode Shadows {
                set {
                        if (SetProperty("_Shadows", (float)value)) {
                                SetKeyword("_SHADOWS_CLIP", value == ShadowMode.Clip);
                                SetKeyword("_SHADOWS_DITHER", value == ShadowMode.Dither);
                        }
                }
        }
        
        public override void OnGUI (MaterialEditor materialEditor, MaterialProperty[] properties) {
                EditorGUI.BeginChangeCheck();
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
                if (EditorGUI.EndChangeCheck()) {
                        SetShadowCasterPass();
                }
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
        
        void SetShadowCasterPass () {
                MaterialProperty shadows = FindProperty("_Shadows", m_MaterialProperties, false);
                if (shadows == null || shadows.hasMixedValue) {
                        return;
                }
                bool enabled = shadows.floatValue < (float)ShadowMode.Off;
                foreach (Material m in m_Materials) {
                        m.SetShaderPassEnabled("ShadowCaster", enabled);
                }
        }

        bool SetProperty (string name, float value) 
        {
                MaterialProperty property = FindProperty(name, m_MaterialProperties, false);
                if (property != null) {
                        property.floatValue = value;
                        return true;
                }
                return false;
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