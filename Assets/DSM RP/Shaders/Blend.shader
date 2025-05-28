Shader "DSM RP/Blend"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("Dst Blend", Float) = 0
        [Enum(UnityEngine.Rendering.BlendOp)] _BlendOp("Blend Operation", Float) = 0
    }
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "PostEffect/PostEffectCommon.hlsl"
        #include "BlendPass.hlsl"
        ENDHLSL

        
        Pass
        {
            Name "Blend"
            Tags {"LightMode" = "DSMLit"}
            Blend [_SrcBlend] [_DstBlend]
            BlendOp [_BlendOp]
            
            HLSLPROGRAM
            #pragma vertex DefaultPostEffectVertex
            #pragma fragment BlendPassFragment
            #pragma target 5.0
            ENDHLSL
        }
    }
}