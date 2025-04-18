#ifndef __LIGHTING_HLSL__
#define __LIGHTING_HLSL__

#include "Surface.hlsl"
#include "Light.hlsl"
#include "BRDF.hlsl"
#include "GI.hlsl"

float3 IncomingLight(Surface surface, Light light)
{
    return saturate(dot(surface.normal, light.direction) * light.attenuation) * light.color;
}

float3 GetLighting (Surface surface, BRDF brdf, Light light) {
    return IncomingLight(surface, light) * DirectBRDF(surface, brdf, light);
}

float3 GetLighting(Surface surfaceWS, BRDF brdf, GI gi)
{
    ShadowData shadowData = GetShadowData(surfaceWS);
    float3 col = gi.diffuse * brdf.diffuse;
    for (int i = 0; i < GetDirectionalLightCount(); ++i) {
        Light light = GetDirectionalLight(i, surfaceWS, shadowData);
        col += GetLighting(surfaceWS, brdf, light);
    }
    return col;
}

#endif
