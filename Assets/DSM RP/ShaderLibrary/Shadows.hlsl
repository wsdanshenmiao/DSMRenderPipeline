#ifndef __SHADOWS__HLSL__
#define __SHADOWS__HLSL__

#define MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

// 阴影数据
struct DirectionalShadowData
{
    float strength;
    float tileindex;
    float normalBias;
};

struct ShadowData
{
    int cascadeIndex;
    float strength;
};

// Shadowmap
TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
// 比较采样器，和DX中的 SamplerComparisonState
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);

CBUFFER_START(_CustomShadows)
    float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
    float4x4 _DirectionalShadowMatrices[MAX_SHADOWED_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
    float4 _ShadowDistanceFade;
    float4 _CascadeData[MAX_CASCADE_COUNT];
    int _CascadeCount;
CBUFFER_END

float FadedShadowStrength (float dist, float scale, float fade)
{
    return saturate((1 - dist * scale) * fade);
}

float SampleDirectionalShadowAtlas(float3 posSTS)
{
   return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, posSTS);
}

float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData shadowData, Surface surface)
{
    [branch]
    if (directional.strength <= 0) {
        return 1;
    }

    // 计算阴影的偏移, 将位置提高一个纹素
    float3 normalBias = surface.normal * (directional.normalBias * _CascadeData[shadowData.cascadeIndex].y);
    float4 position = float4(surface.position + normalBias, 1);
    // 从世界坐标变换到光源空间
    float3 posSTS = mul(_DirectionalShadowMatrices[directional.tileindex], position).xyz;
    float shadow = SampleDirectionalShadowAtlas(posSTS);
    return lerp(1, shadow, directional.strength);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData shadowData;
    shadowData.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    // 寻找包含片元的剔除球
    int i;
    for (i = 0; i < _CascadeCount; i++) {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        [flatten]
        if (distanceSqr < sphere.w) {
            [flatten]
            if (i == _CascadeCount - 1) {
                shadowData.strength = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
            }
            break;
        }
    }
    // 超出级联范围不渲染阴影
    [flatten]
    if (i >= _CascadeCount) {
        shadowData.strength = 0;
    }
    shadowData.cascadeIndex = i;
    return shadowData;
}

#endif
