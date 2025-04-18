#ifndef __UNLITINPUT__HLSL__
#define __UNLITINPUT__HLSL__

UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseTex_ST)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

TEXTURE2D(_BaseTex);
SAMPLER(sampler_BaseTex);

float2 TransformBaseUV(float2 uv)
{
    return uv * _BaseTex_ST.xy + _BaseTex_ST.zw;
}

float4 GetBase(float2 uv)
{
    float4 color = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    float4 texCol = SAMPLE_TEXTURE2D(_BaseTex, sampler_BaseTex, uv);
    return color * texCol;
}

float3 GetEmission (float2 uv)
{
    return GetBase(uv).rgb;
}

float GetCutoff(float2 uv)
{
    return UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff);
}


#endif