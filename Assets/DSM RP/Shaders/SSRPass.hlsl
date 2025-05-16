#ifndef __SSRPASS_HLSL__
#define __SSRPASS_HLSL__

#include "../ShaderLibrary/Common.hlsl"

TEXTURE2D(_NormalTexture);
SAMPLER(sampler_NormalTexture);

CBUFFER_START(_SSRCONSTANTS)
    int _RayMarchingMaxCount;
    float _RayMarchingStep;
    float _HitThreshold;
CBUFFER_END

bool GetCurrDepthAndUV(float3 currPos, out float currDepth, out float2 uv)
{
    // 变换到NDC空间
    float4 posCS = mul(UNITY_MATRIX_VP, float4(currPos, 1));
    currDepth = posCS.w; // 根据投影矩阵可以得到齐次裁剪空间下的 w 就是视图空间下的深度
    posCS.xyz /= posCS.w;
        
    // _ProjectionParams 为 1 或 -1
    // 重建 uv 来获取采样点的深度及颜色
    uv = float2(posCS.x, posCS.y * _ProjectionParams.x) * 0.5 + 0.5;
    
    return (0 <= uv.x && uv.x <= 1 && 0 <= uv.y && uv.y <= 1);
}

// 二分查找，来准确定位反射光线打中的像素
float4 SSRBinarySearch(Ray ray)
{
    float step = _RayMarchingStep * 0.5;
    float3 prePos = ray.origin;
    
    [loop]
    for (float3 curr = ray.origin; step > _RayMarchingStep * 0.075; ) {
        prePos = curr;
        curr += ray.rayDir * step;
        float currDepth;
        float2 uv;
        GetCurrDepthAndUV(curr, currDepth, uv);
        float depthTex = GetCameraLinearDepth(uv);
        [branch]
        if (depthTex < currDepth) {
            [flatten]
            if (currDepth < depthTex + _HitThreshold) {  // 在阈值范围内
                return GetCameraColor(uv);
            }
            else {
                curr = prePos;
                step *= 0.5;
            }
        }
    }
    return 0;
}

// 光线步进主体
float4 SSRRayMarching(Ray ray)
{
    float3 currPos = ray.origin;
    float3 prePos = currPos;
    
    [loop]
    for (int i = 0; i < _RayMarchingMaxCount; ++i) {
        prePos = currPos;
        currPos += ray.rayDir * _RayMarchingStep;

        // 获取当前位置的深度
        float currDepth;
        float2 uv;
        if (!GetCurrDepthAndUV(currPos, currDepth, uv)) break;

        float depthTex = GetCameraLinearDepth(uv);
        
        // 射线已经穿过了物体
        [branch]
        if (depthTex < currDepth) {
            [flatten]
            if (currDepth < depthTex + _HitThreshold) {  // 在阈值范围内
                return GetCameraColor(uv);
            }
            else {
                ray.origin = prePos;
                return SSRBinarySearch(ray); // 回退并继续查找
            }
        }
    }

    return float4(0, 0, 0, 1);
}


float4 SSRPassFragment(Varyings input) : SV_TARGET
{
    float3 posW = GetWorldPosition(input.uv);
    
    // 获取RayMarching所需的信息
    float3 normal = SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, input.uv).xyz;
    if (all(normal == 0)) return 0;
    
    float3 viewDir = normalize(posW - _WorldSpaceCameraPos.xyz);
    float3 rayDir = normalize(reflect(viewDir, normal));
    
    float4 baseCol = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_linear_clamp, input.uv);
    
    Ray ray;
    ray.rayDir = rayDir;
    ray.origin = posW;
    float4 reflectCol = SSRRayMarching(ray);
    
    return lerp(baseCol, reflectCol, reflectCol);
}


#endif