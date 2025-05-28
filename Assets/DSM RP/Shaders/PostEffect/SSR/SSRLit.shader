Shader "DSM RP/SSRLit"
{
    Properties
    {
        // 纹理
        _BaseTex("Base Texture", 2D) = "white"{}
        _BaseColor("Base Color", Color) = (0.5,0.5,0.5,1)
        // 混合因子
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        // 是否开启深度写入
        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1
        // Alpha 裁剪
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        // 是否开启 Alpha 测试
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
  		[Toggle(_PREMULTIPLY_ALPHA)] _PremulAlpha ("Premultiply Alpha", Float) = 0
        // 材质金属性
        _Metallic ("Metallic", Range(0, 1)) = 0
        // 材质光滑程度
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
        // 可控制是否接收阴影
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
        // 散射贴图及散射光
        [NoScaleOffset] _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("Emission Color", Color) = (0,0,0,0)
        
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
		[HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
    }
    SubShader
    {
        UsePass "DSM RP/Lit/Lit"

        Pass
        {
            Name "SSRMaskPass"
            Tags {"LightMode" = "DSMLit"}
            
            Cull Off
            ZTest Always
            ZWrite Off
            
            HLSLPROGRAM
            #include "../PostEffectCommon.hlsl"
            #include "../../../ShaderLibrary/Common.hlsl"
            #pragma DefaultPostEffectVertex
            #pragma SSRMaskFragment
            
            UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
                UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
            UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)
            
            float2 SSRMaskFragment(Varyings i) : SV_TARGET
            {
                return float2(1, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness));
            }
            
            ENDHLSL
        }
    }
}