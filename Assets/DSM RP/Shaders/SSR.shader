Shader "DSM RP/SSR"
{
    Properties
    {
        _StencilRef("StencilRef", Range(0, 255)) = 200
    }
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "../ShaderLibrary/PostEffectCommon.hlsl"
        #include "SSRPass.hlsl"
        ENDHLSL


        
        Pass
        {
            Stencil
             {
/*                 Ref [_StencilRef]
                 Comp Equal
                 Pass Keep*/
             }
            Name "SSR"
            
            HLSLPROGRAM
            #pragma vertex DefaultPostEffectVertex
            #pragma fragment SSRPassFragment
            #pragma target 5.0
            ENDHLSL
        }
    }
}