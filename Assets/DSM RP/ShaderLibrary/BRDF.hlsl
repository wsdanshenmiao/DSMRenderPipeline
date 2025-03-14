#ifndef __BRDF__HLSL__
#define __BRDF__HLSL__

#include "Surface.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

#define MIN_REFLECTIVITY 0.04

struct BRDF
{
    float3 diffuse;
    float3 specular;
    float roughness;
};

float OneMinusReflectivity(float metallic)
{
    float range = 1 - MIN_REFLECTIVITY;
    return range * (1 - metallic);
}

BRDF GetBRDF(Surface surface, bool applyAlphaToDiffuse = false)
{
    float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
    // 感知上的粗糙程度
    float perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    BRDF brdf;
    brdf.diffuse = surface.color * oneMinusReflectivity;    // 透射系数
    if (applyAlphaToDiffuse) {
        brdf.diffuse *= surface.alpha;
    }
    brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);   // 反射系数
    brdf.roughness = PerceptualRoughnessToRoughness(perceptualRoughness);   // 粗糙程度
    return brdf;
}

// CookTorrance BRDF的变体
// r^2 / (d^2 * max(0.1, pow(dot(L, H), 2)) * n)
// d = pow(dot(N, H), 2) * (r^2 - 1) + 1.0001
// n = 4r + 2
float SpecularStrength(Surface surface, BRDF brdf, Light light)
{
    float3 h = normalize(surface.viewDirection + light.direction);
    float r2 = Square(brdf.roughness);
    float d = Square(saturate(dot(surface.normal, h))) * (r2 - 1) + 1.00001;
    float n = 4 * brdf.roughness + 2;
    float lh2 = Square(saturate(dot(light.direction, h)));
    return r2 / Square(d) * max(0.1, lh2) * n;
}

float3 DirectBRDF (Surface surface, BRDF brdf, Light light)
{
    return SpecularStrength(surface, brdf, light) * brdf.specular + brdf.diffuse;
}

#endif