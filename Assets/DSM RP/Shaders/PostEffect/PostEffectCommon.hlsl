#ifndef __POSTEFFECTCOMMON_HLSL__
#define __POSTEFFECTCOMMON_HLSL__

CBUFFER_START(_POST_EFFECT)
CBUFFER_END

struct Ray
{
    float3 rayDir;
    float3 origin;
};

struct Varyings
{
    float4 posCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

/*
 *  1
 *  |\
 *  | \
 *  |  \
 *  |___ \
 *  0       2
 */
// 覆盖全屏的三角形
Varyings DefaultPostEffectVertex(uint vertexID : SV_VertexID)
{
    Varyings output;

    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    output.uv = uv;
    output.posCS = float4(output.uv * 2.0 - 1.0, 0, 1.0);
    
    [flatten]
    if (_ProjectionParams.x < 0) {
        output.uv.y = 1 - output.uv.y;
    }
    
    return output;
}


#endif