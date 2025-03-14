#ifndef __LIGHTING_HLSL__
#define __LIGHTING_HLSL__

#include "Surface.hlsl"
#include "Light.hlsl"
#include "BRDF.hlsl"

float3 IncomingLight(Surface surface, Light light)
{
    return saturate(dot(surface.normal, light.direction)) * light.color;
}

float3 GetLighting (Surface surface, BRDF brdf, Light light) {
    return IncomingLight(surface, light) * brdf.diffuse * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surface, BRDF brdf)
{
    float3 col = 0;
    for (int i = 0; i < GetDirectionalLightCount(); ++i) {
        col += GetLighting(surface, brdf, GetDirectionalLight(i));
    }
    return col;
}

#endif
