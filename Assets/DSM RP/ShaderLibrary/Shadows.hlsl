#ifndef __SHADOWS__HLSL__
#define __SHADOWS__HLSL__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

// 使用帐篷过滤器对阴影进行过滤
#if defined(_DIRECTIONAL_PCF3)
    #define DIRECTIONAL_FILTER_SAMPLES 4
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
    #define DIRECTIONAL_FILTER_SAMPLES 9
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
    #define DIRECTIONAL_FILTER_SAMPLES 16
    #define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif


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
    float cascadeBlend; // 用于缓解不同级联之间的过渡带
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
    float2 _ShadowAtlasSize;
CBUFFER_END

float FadedShadowStrength (float dist, float scale, float fade)
{
    return saturate((1 - dist * scale) * fade);
}

float SampleDirectionalShadowAtlas(float3 posSTS)
{
   return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, posSTS);
}

float FilterDirectionalShadow(float3 posSTS)
{
    #if defined(DIRECTIONAL_FILTER_SETUP)
    // 获取采样信息
    float weights[DIRECTIONAL_FILTER_SAMPLES];
    float2 positions[DIRECTIONAL_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.yyxx;
    DIRECTIONAL_FILTER_SETUP(size, posSTS, weights, positions);

    // 进行 PCF
    float shadow = 0;
    [flatten]
    for (int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; ++i) {
        shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i], posSTS.z));
    }
    return shadow;
    #else
    return SampleDirectionalShadowAtlas(posSTS);
    #endif
}

float GetDirectionalShadowAttenuation(DirectionalShadowData directional, ShadowData shadowData, Surface surface)
{
    // 可控制是否接收阴影
    #if !defined(_RECEIVE_SHADOWS)
    return 1.0;
    #endif
    
    [branch]
    if (directional.strength <= 0) {
        return 1;
    }

    // 计算阴影的偏移, 将位置提高一个纹素
    float3 normalBias = surface.normal * (directional.normalBias * _CascadeData[shadowData.cascadeIndex].y);
    float4 position = float4(surface.position + normalBias, 1);
    // 从世界坐标变换到光源空间
    float3 posSTS = mul(_DirectionalShadowMatrices[directional.tileindex], position).xyz;
    float shadow = FilterDirectionalShadow(posSTS);
    
    // 对不同级联之间的交界线进行过度,会造成较大性能开销
    [branch]
    if (shadowData.cascadeBlend < 1) { // 不为 1 时则进行过度
        // 获取下一个级联下该像素的阴影
        normalBias = surface.normal * (directional.normalBias * _CascadeData[shadowData.cascadeIndex + 1].y);
        position = float4(surface.position + normalBias, 1);
        posSTS = mul(_DirectionalShadowMatrices[directional.tileindex + 1], position).xyz;
        // 对两个级联的结果进行插值
        shadow = lerp(FilterDirectionalShadow(posSTS), shadow, shadowData.cascadeBlend);
    }
    return lerp(1, shadow, directional.strength);
}

ShadowData GetShadowData(Surface surfaceWS)
{
    ShadowData shadowData;
    // 最远边界的过度
    shadowData.strength = FadedShadowStrength(surfaceWS.depth, _ShadowDistanceFade.x, _ShadowDistanceFade.y);
    shadowData.cascadeBlend = 1;
    // 寻找包含片元的剔除球
    int i;
    for (i = 0; i < _CascadeCount; i++) {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(surfaceWS.position, sphere.xyz);
        [flatten]
        if (distanceSqr < sphere.w) {
            // 级联边缘的过度
            float fade = FadedShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistanceFade.z);
            [flatten]
            if (i == _CascadeCount - 1) {
                shadowData.strength *= fade;
            }
            else {
                shadowData.cascadeBlend = fade;
            }
            break;
        }
    }
    // 超出级联范围不渲染阴影
    [flatten]
    if (i >= _CascadeCount) {
        shadowData.strength = 0;
    }
    #if defined(_CASCADE_BLEND_DITHER)
    else if (shadowData.cascadeBlend < surfaceWS.dither) {
        // 进行抖动，可能计算下一个级联的结果，缓解级联之间的过度问题
        i += 1;
    }
    #endif
    shadowData.cascadeIndex = i;

    // 不使用混合
    #if !defined(_CASCADE_BLEND_SOFT)
    shadowData.cascadeBlend = 1;
    #endif
    
    return shadowData;
}

#endif
