#ifndef __LIGHT__HLSL__
#define __LIGHT__HLSL__

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_OTHER_LIGHT_COUNT 256


CBUFFER_START(_DSMLight)
    int _DirectionalLightCount;
    int _OtherLightCount;
CBUFFER_END

struct DirectionalLightData
{
    float4 color;
    float4 direction;
    float4 shadowData;
};

struct OtherLightData
{
    float4 color;
    float4 direction;
    float4 positionAndRange;
    float4 shadowData;
    float4 spotAngle;
};

StructuredBuffer<DirectionalLightData> _DirectionalLightDatas;
StructuredBuffer<OtherLightData> _OtherLightDatas;

struct Light
{
    float3 color;
    float3 direction;
    float attenuation;
};

DirectionalShadowData GetDirectionalShadowData(int index, ShadowData shadowData)
{
    DirectionalShadowData data;
    float4 lightShadowData = _DirectionalLightDatas[index].shadowData;
    data.strength = lightShadowData.x * shadowData.strength;
    data.tileindex = lightShadowData.y + shadowData.cascadeIndex;
    data.normalBias = lightShadowData.z;
    return data;
}

int GetDirectionalLightCount()
{
    return _DirectionalLightCount;
}

int GetOtherLightCount()
{
    return _OtherLightCount;
}


// 计算光源的平方衰减
float GetSquareFalloffAttenuation(float3 posToLight, float powInvRange)
{
    float distSqr = dot(posToLight, posToLight);
    float factor = distSqr * powInvRange;
    float smoothFactor = max(1 - factor * factor, 0);
    return smoothFactor * smoothFactor / max(distSqr, 1e-4);
}

float GetSpotAngleAttenuation(float3 l, float3 lightDir, float innerAngle, float outerAngle)
{
    float cosOuter = cos(outerAngle);
    float spotScale = 1.0 / max(cos(innerAngle) - cosOuter, 1e-4);
    float spotOffset = -cosOuter * spotScale;

    float cd = dot(lightDir, l);
    float attenuation = saturate(cd * spotScale + spotOffset);
    return attenuation * attenuation;
}


Light GetDirectionalLight(int index, Surface surface, ShadowData shadowData)
{
    Light light;
    light.color = _DirectionalLightDatas[index].color.rgb;
    light.direction = _DirectionalLightDatas[index].direction.xyz;
    DirectionalShadowData data = GetDirectionalShadowData(index, shadowData);
    light.attenuation = GetDirectionalShadowAttenuation(data, shadowData, surface);
    return light;
}

Light GetOtherLight(int index, Surface surface, ShadowData shadowData)
{
    OtherLightData lightData = _OtherLightDatas[index];
    float3 pos = lightData.positionAndRange.xyz;
    float powInvRange = lightData.positionAndRange.w;
    float2 spotAngle = lightData.spotAngle.xy;
    Light light;
    light.color = lightData.color.rgb;
    float3 posToLight = pos - surface.position;
    float3 l = normalize(posToLight);
    light.direction = l;
    light.attenuation = GetSquareFalloffAttenuation(posToLight, powInvRange);
    light.attenuation *= GetSpotAngleAttenuation(l, -lightData.direction.xyz, spotAngle.x, spotAngle.y);
    return light;
}

#endif