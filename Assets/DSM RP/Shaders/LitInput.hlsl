#ifndef __LITINPUT__HLSL__
#define __LITINPUT__HLSL__

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseTex_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
    UNITY_DEFINE_INSTANCED_PROP(float, _Metallic)
    UNITY_DEFINE_INSTANCED_PROP(float, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


TEXTURE2D(_BaseTex);
TEXTURE2D(_EmissionMap);
SAMPLER(sampler_BaseTex);

float2 TransformBaseUV(float2 uv)
{
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseTex_ST);
    return uv * baseST.xy + baseST.zw;
}

float4 GetBase(float2 uv)
{
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    float4 texCol = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, uv);
    return color * texCol;
}

float3 GetEmission(float2 uv)
{
    float4 map = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseTex, uv);
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _EmissionColor);
    return map.rgb * color.rgb;
}

float GetCutoff(float2 uv)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}

float GetMetallic(float2 uv)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
}

float GetSmoothness(float2 uv)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
}

#endif