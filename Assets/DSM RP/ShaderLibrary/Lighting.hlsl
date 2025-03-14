#ifndef __LIGHTING_HLSL__
#define __LIGHTING_HLSL__

#include "Surface.hlsl"
#include "Light.hlsl"


float3 IncomingLight(Surface surface, Light light)
{
    return saturate(dot(surface.normal, light.direction)) * light.color;
}

float3 GetLighting (Surface surface, Light light) {
    return IncomingLight(surface, light) * surface.color;
}

float3 GetLighting(Surface surface)
{
    float3 col = 0;
    for (int i = 0; i < GetDirectionalLightCount(); ++i) {
        col += GetLighting(surface, GetDirectionalLight(i));
    }
    return col;
}

#endif
