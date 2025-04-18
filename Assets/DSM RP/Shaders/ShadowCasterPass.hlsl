#ifndef __SHADOWCASTERPASSPASS__HLSL__
#define __SHADOWCASTERPASSPASS__HLSL__

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

Varyings ShadowCasterPassVertex(Attributes i)
{
    Varyings o;
    
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i, o);
    
    float3 posWS = TransformObjectToWorld(i.posOS);
    o.posCS = TransformWorldToHClip(posWS);
    o.uv = TransformBaseUV(i.uv);

    // 避免由于近平面过于靠前而导致阴影裁剪
    #if UNITY_REVERSED_Z
    o.posCS.z =
        min(o.posCS.z, o.posCS.w * UNITY_NEAR_CLIP_VALUE);
    #else
    o.posCS.z =
        max(o.posCS.z, o.posCS.w * UNITY_NEAR_CLIP_VALUE);
    #endif
    
    return o;
}

void ShadowCasterPassFragment(Varyings i)
{
    UNITY_SETUP_INSTANCE_ID(i);
    float4 base = GetBase(i.uv);
    // 为保证阴影正确需要加上剔除
    #if defined(_SHADOWS_CLIP)
    clip(base.a - GetCutoff(i.uv));
    #elif defined(_SHADOWS_DITHER)  // 使用抖动代替剔除
    float dither = InterleavedGradientNoise(i.posCS.xy, 0);
    clip(base.a - dither);
    #endif
}

#endif
