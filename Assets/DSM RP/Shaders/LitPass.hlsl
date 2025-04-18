#ifndef __LITPASS__HLSL__
#define __LITPASS__HLSL__

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"


struct Attributes
{
    float3 posOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    GI_ATTRIBUTE_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 posCS : SV_POSITION;
    float3 posWS : TEXCOORD1;
    float3 normalWS : NORMAL;
    float2 uv : TEXCOORD0;
    GI_VARYINGS_DATA
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes i)
{
    Varyings o;
    
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i, o);
    
    o.posWS = TransformObjectToWorld(i.posOS);
    o.posCS = TransformWorldToHClip(o.posWS);
    o.normalWS = TransformObjectToWorldNormal(i.normalOS);
    o.uv = TransformBaseUV(i.uv);

    TRANSFER_GI_DATA(i, o);
    
    return o;
}

float4 LitPassFragment(Varyings i) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i)
    
    float4 col = GetBase(i.uv);
    
    // 由于会 Alpha 测试会阻止 EarlyZ 等优化，所以选择性开启
    #if defined(_CLIPPING)
    clip(col.a - GetCutoff(i.uv));
    #endif

    // 获取物体的表面属性
    Surface surface;
    surface.position = i.posWS;
    surface.normal = normalize(i.normalWS);
    surface.color = col.rgb;
    surface.alpha = col.a;
    surface.metallic = GetMetallic(i.uv);
    surface.smoothness = GetSmoothness(i.uv);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - i.posWS);
    surface.depth = -TransformWorldToView(i.posWS).z;
    surface.dither = InterleavedGradientNoise(i.posCS.xy, 0);

#if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface, true);
#else
    BRDF brdf = GetBRDF(surface);
#endif

    GI gi = GetGI(GI_FRAGMENT_DATA(i), surface);
    col.rgb = GetLighting(surface, brdf, gi);
    col.rgb += GetEmission(i.uv);
    
    return float4(col.rgb, surface.alpha);
}

#endif
