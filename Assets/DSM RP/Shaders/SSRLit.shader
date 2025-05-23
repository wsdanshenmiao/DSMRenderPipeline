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
        
        // 使用的模板值
        _StencilRef("StencilRef", Range(0, 255)) = 200
        
        [HideInInspector] _MainTex("Texture for Lightmap", 2D) = "white" {}
		[HideInInspector] _Color("Color for Lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "LitInput.hlsl"
        ENDHLSL

        Stencil
        {
            Ref [_StencilRef]
            Comp Always
            Pass Replace
        }
        
        Pass
        {
            Tags {"LightModel" = "DSMLit"}
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            
            
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma shader_feature _CLIPPING
            #pragma shader_feature _RECEIVE_SHADOWS
            #pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
            #pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
            #pragma multi_compile _ LIGHTMAP_ON 
        	#pragma shader_feature _PREMULTIPLY_ALPHA    
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma target 5.0
            #include "LitPass.hlsl"
            ENDHLSL
        }
    }
}