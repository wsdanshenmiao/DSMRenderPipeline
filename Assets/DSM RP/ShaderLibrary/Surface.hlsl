#ifndef __SUFACE__HLSL__
#define __SUFACE__HLSL__

// 物体的表面属性
struct Surface
{
    float3 position;
    float3 normal;
    float alpha;
    float3 color;
    float metallic;
    float3 viewDirection;
    float smoothness;
    float depth;
};

#endif