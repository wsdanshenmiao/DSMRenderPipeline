#ifndef __LITPASS__HLSL__
#define __LITPASS__HLSL__

#include "../ShaderLibrary/Common.hlsl"
#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Lighting.hlsl"


UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseTex_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

TEXTURE2D(_BaseTex);
SAMPLER(sampler_BaseTex);

struct Attributes
{
    float3 posOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 posCS : SV_POSITION;
    float3 posWS : TEXCOORD1;
    float3 normalWS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

Varyings LitPassVertex(Attributes i)
{
    Varyings o;
    
    UNITY_SETUP_INSTANCE_ID(i);
    UNITY_TRANSFER_INSTANCE_ID(i, o);
    
    float4 texST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseTex_ST);
    o.posWS = TransformObjectToWorld(i.posOS);
    o.posCS = TransformWorldToHClip(o.posWS);
    o.normalWS = TransformObjectToWorldNormal(i.normalOS);
    o.uv = i.uv * texST.xy + texST.zw;
    
    return o;
}

float4 LitPassFragment(Varyings i) : SV_TARGET
{
    UNITY_SETUP_INSTANCE_ID(i)

    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    float4 texCol = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, i.uv);
    float4 col = color * texCol;
    
    // 由于会 Alpha 测试会阻止 EarlyZ 等优化，所以选择性开启
    #if defined(_CLIPPING)
    clip(col.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #endif

    // 获取物体的表面属性
    Surface surface;
    surface.position = i.posWS;
    surface.normal = normalize(i.normalWS);
    surface.color = col.rgb;
    surface.alpha = col.a;
    surface.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
    surface.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
    surface.viewDirection = normalize(_WorldSpaceCameraPos - i.posWS);
    surface.depth = -TransformWorldToView(i.posWS).z;

#if defined(_PREMULTIPLY_ALPHA)
    BRDF brdf = GetBRDF(surface, true);
#else
    BRDF brdf = GetBRDF(surface);
#endif

    col.rgb = GetLighting(surface, brdf);
    
    return float4(col.rgb, surface.alpha);
}

#endif
