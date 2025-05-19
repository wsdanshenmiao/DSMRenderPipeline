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

/*
 * return:
 *  true: 在当前位置获取了颜色信息
 *  false: 为获取颜色信息
 */
bool CheckCurrPos(float3 currPos, out float4 outCol, out float depthDis)
{
    outCol = float4(0, 0, 0, 1);
    // 获取当前位置的深度
    float currDepth;
    float2 uv;
    [branch]
    if (!GetCurrDepthAndUV(currPos, currDepth, uv)) {
        depthDis = 1;
        return false;
    }

    float depthTex = GetCameraLinearDepth(uv);
    depthDis = currDepth - depthTex;
    // 射线已经穿过了物体
    [branch]
    if (depthDis > 0) {
        bool inRange = depthDis <  _HitThreshold;  // 在阈值范围内
        outCol = inRange ? GetCameraColor(uv) : outCol;
        return inRange;
    }
    return false;
}

/*
 * return:
 *  true: 使用完不仅次数或深度达到阈值内，不需要进行二分查找
 *  false: 深度差不再阈值内，需要二分查找
 */
bool RayMarching(Ray ray, float marchCount, float marchStep, out float4 outCol, out float3 endPos)
{
    float3 currPos = ray.origin;
    outCol = float4(0, 0, 0, 1);
    
    [loop]
    for (int i = 0; i < marchCount; ++i) {
        endPos = currPos;
        currPos += ray.rayDir * marchStep;

        float depthDis;
        // 当 depthDis > 0 时可以直接结束
        bool getResult = CheckCurrPos(currPos, outCol, depthDis);
        if (depthDis > 0) return getResult;
    }
    return true;
}

// 二分查找，来准确定位反射光线打中的像素
float4 BinarySearch(Ray ray)
{
    static const int MaxBinarySearchCount = 5;   // 限制查找次数
    
    float step = _RayMarchingStep * 0.5;
    float3 currPos = ray.origin;
    float4 outCol = float4(0, 0, 0, 1);

    [unroll]
    for (int i = 0; i < MaxBinarySearchCount; i++) {
        float3 prePos = currPos;
        currPos += ray.rayDir * step;

        float depthDis;
        if (CheckCurrPos(currPos, outCol, depthDis)) break;
        [flatten]
        if (depthDis > 0) { // 回退
            currPos = prePos;
            step *= 0.5;
        }
    }
    return outCol;
}

// 光线步进主体
float4 WorldSpaceRayMarching(Ray ray)
{
    float4 outCol = float4(0, 0, 0, 1);
    float3 endPos = ray.origin;
    if (!RayMarching(ray, _RayMarchingMaxCount, _RayMarchingStep, outCol, endPos)) {  // 未找到则进行二分查找
        ray.origin = endPos;
        outCol = BinarySearch(ray);
    }

    return outCol;
}


float4 SSRPassFragment(Varyings input) : SV_TARGET
{
    float3 posW = GetWorldPosition(input.uv);
    
    // 获取RayMarching所需的信息
    float3 normal = SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, input.uv).xyz;
    [branch]
    if (all(normal == 0)) return 0;
    
    float3 viewDir = normalize(posW - _WorldSpaceCameraPos.xyz);
    float3 rayDir = normalize(reflect(viewDir, normal));
    
    float4 baseCol = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_linear_clamp, input.uv);
    
    Ray ray;
    ray.rayDir = rayDir;
    ray.origin = posW;
    float4 reflectCol = WorldSpaceRayMarching(ray);

    return baseCol + reflectCol;
    return lerp(baseCol, reflectCol, reflectCol);
}


#endif