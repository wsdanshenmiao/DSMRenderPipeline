Shader "DSM RP/SSR"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
        #include "../../../ShaderLibrary/Common.hlsl"
        #include "../PostEffectCommon.hlsl"
        #include "SSRPass.hlsl"
        ENDHLSL

        
        Pass
        {
            Name "SSR"
            Tags {"LightMode" = "DSMLit"}
            
            HLSLPROGRAM
            #pragma multi_compile _ SCREENSPACE SCREENSPACEHIEZ
            #pragma vertex DefaultPostEffectVertex
            #pragma fragment SSRPassFragment
            #pragma target 5.0
            ENDHLSL
        }
    }
}