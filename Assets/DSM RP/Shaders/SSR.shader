Shader "DSM RP/SSR"
{
    Properties
    {

    }
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "../ShaderLibrary/PostEffectCommon.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "SSR"
            
            HLSLPROGRAM 
            #pragma vertex DefaultPostEffectVertex
            #pragma fragment SSRPassFragment
            #pragma target 5.0
            #include "SSRPass.hlsl"
            ENDHLSL
        }
    }
}