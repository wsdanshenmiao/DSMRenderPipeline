#ifndef __UNLITPASS__HLSL__
#define __UNLITPASS__HLSL__

#include "../ShaderLibrary/Common.hlsl"



struct Attributes
{
    float3 posOS : POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 posCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings UnlitPassVertex(Attributes i)
{
    Varyings o;
    
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i, o);
    
    float3 posWS = TransformObjectToWorld(i.posOS);
    o.posCS = TransformWorldToHClip(posWS);
    o.uv = TransformBaseUV(i.uv);
    
    return o;
}

float4 UnlitPassFragment(Varyings i) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i)

    float4 col = GetBase(i.uv);
    
    // 由于会 Alpha 测试会阻止 EarlyZ 等优化，所以选择性开启
    #if defined(_CLIPPING)
    clip(col.a - GetCutoff(i.uv));
    #endif
    return col;
}

#endif
