Shader "DSM RP/Unlit"
{
    Properties
    {
        // 纹理
        _BaseTex("Base Texture", 2D) = "white"{}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        // 混合因子
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        // 是否开启深度写入
        [Enum(Off, 0, On, 1)] _ZWrite("Z Write", Float) = 1
        // Alpha 裁剪
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        // 是否开启 Alpha 测试
		[Toggle(_CLIPPING)] _Clipping ("Alpha Clipping", Float) = 0
        [KeywordEnum(On, Clip, Dither, Off)] _Shadows ("Shadows", Float) = 0
        // 可控制是否接收阴影
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows ("Receive Shadows", Float) = 1
    }
    SubShader
    {
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "UnlitInput.hlsl"
        ENDHLSL
        
        Pass
        {
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            
            
            HLSLPROGRAM
            #pragma multi_compile_instancing
            #pragma shader_feature _CLIPPING
            #pragma vertex UnlitPassVertex
            #pragma fragment UnlitPassFragment
            #include "UnlitPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Tags {"LightMode" = "ShadowCaster"}
            
            // 禁止颜色写入
            ColorMask 0
            
            HLSLPROGRAM
            #pragma target 3.5
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }
    }
}
