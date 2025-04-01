#ifndef __LIGHT__HLSL__
#define __LIGHT__HLSL__

#define MAX_DIRECTIONAL_LIGHT_COUNT 4

CBUFFER_START(_DSMLight)
    int _DirectionalLightCount;
    float4 _DirectionalLightColors[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightDirections[MAX_DIRECTIONAL_LIGHT_COUNT];
    float4 _DirectionalLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
CBUFFER_END

struct Light
{
    float3 color;
    float3 direction;
    float attenuation;
};

DirectionalShadowData GetDirectionalShadowData(int index, ShadowData shadowData)
{
    DirectionalShadowData data;
    data.strength = _DirectionalLightShadowData[index].x;
    data.tileindex = _DirectionalLightShadowData[index].y + shadowData.cascadeIndex;
    return data;
}

int GetDirectionalLightCount ()
{
    return _DirectionalLightCount;
}

Light GetDirectionalLight(int index, Surface surface, ShadowData shadowData)
{
    Light light;
    light.color = _DirectionalLightColors[index].rgb;
    light.direction = _DirectionalLightDirections[index].xyz;
    DirectionalShadowData data = GetDirectionalShadowData(index, shadowData);
    light.attenuation = GetDirectionalShadowAttenuation(data, surface);
    return light;
}

#endif