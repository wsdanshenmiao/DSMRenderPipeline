Shader "DSM RP/CameraDebugger"
{
    SubShader
    {
        Cull Off
        ZTest Always
        ZWrite Off
        
        HLSLINCLUDE
        #include "../ShaderLibrary/Common.hlsl"
        #include "PostEffect/PostEffectCommon.hlsl"
        #include "CameraDebuggerPass.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "CameraDebugger"
            
			Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex DefaultPostEffectVertex
            #pragma fragment CameraDebuggerPassFragment
            #pragma target 5.0
            ENDHLSL
        }
    }
}