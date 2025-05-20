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
    float4 posCS = mul(UNITY_MATRIX_P, float4(currPos, 1));
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


float4 SSRPassWorldSpaceFragment(Varyings input) : SV_TARGET
{
    // 获取RayMarching所需的信息
    float3 normal = SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, input.uv).xyz;
    [branch]
    if (all(normal == 0)) return 0;

    float3 posVS = GetViewPosition(input.uv);
    float3 viewDir = normalize(posVS);
    normal = normalize(mul((float3x3)unity_MatrixV, normal));
    float3 rayDir = normalize(reflect(viewDir, normal));
    
    float4 baseCol = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_linear_clamp, input.uv);
    
    Ray ray;
    ray.rayDir = rayDir;
    ray.origin = posVS;
    float4 reflectCol = WorldSpaceRayMarching(ray);

    return baseCol + reflectCol;
    return lerp(baseCol, reflectCol, reflectCol);
}


float4 SSRPassScreenSpaceFragment(Varyings input) : SV_TARGET
{
    float3 normal = SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, input.uv).xyz;
    [branch]
    if (all(normal == 0)) return 0;

    // 计算世界空间下的反射向量
    float3 viewDir = normalize(GetWorldPosition(input.uv) - GetCameraPos());
    float3 rayDirWS = normalize(reflect(viewDir, normal));

    // 转换到视图空间
    float2 rayDirVS = normalize(mul((float3x3)UNITY_MATRIX_V, rayDirWS).xy);
    
    
}

/*
5 bool traceScreenSpaceRay##numLayers(point3 csOrig, vec3 csDir, mat4x4 proj,
6 sampler2D csZBuffer, vec2 csZBufferSize, float zThickness,
7 const bool csZBufferIsHyperbolic, vec3 clipInfo, float nearPlaneZ,
8 float stride, float jitter, const float maxSteps, float maxDistance,
9 out point2 hitPixel, out int hitLayer, out point3 csHitPoint) {
    10
    11 // Clip to the near plane
    12 float rayLength = ((csOrig.z + csDir.z * maxDistance) > nearPlaneZ) ?
    13 (nearPlaneZ - csOrig.z) / csDir.z : maxDistance;
    14 point3 csEndPoint = csOrig + csDir * rayLength;
    15 hitPixel = point2(-1, -1);
    16
    17 // Project into screen space
    18 vec4 H0 = proj * vec4(csOrig, 1.0), H1 = proj * vec4(csEndPoint, 1.0);
    19 float k0 = 1.0 / H0.w, k1 = 1.0 / H1.w;
    20 point3 Q0 = csOrig * k0, Q1 = csEndPoint * k1;
    21
    22 // Screen-space endpoints
    23 point2 P0 = H0.xy * k0, P1 = H1.xy * k1;
    24
    25 // [ Optionally clip here using listing 4 ]
    26
    27 P1 += vec2((distanceSquared(P0, P1) < 0.0001) ? 0.01 : 0.0);
    28 vec2 delta = P1 - P0;
    29
    30 bool permute = false;
    31 if (abs(delta.x) < abs(delta.y)) {
        32 permute = true;
        33 delta = delta.yx; P0 = P0.yx; P1 = P1.yx;
        34 }
    35
    36 float stepDir = sign(delta.x), invdx = stepDir / delta.x;
    37
    38 // Track the derivatives of Q and k.
    39 vec3 dQ = (Q1 - Q0) * invdx;
    40 float dk = (k1 - k0) * invdx;
    41 vec2 dP = vec2(stepDir, delta.y * invdx);
    42
    43 dP *= stride; dQ *= stride; dk *= stride;
    44 P0 += dP * jitter; Q0 += dQ * jitter; k
// Slide P from P0 to P1, (now-homogeneous) Q from Q0 to Q1, k from k0 to k1
47 point3 Q = Q0; float k = k0, stepCount = 0.0, end = P1.x * stepDir;
48 for (point2 P = P0;
49 ((P.x * stepDir) <= end) && (stepCount < maxSteps);
50 P += dP, Q.z += dQ.z, k += dk, stepCount += 1.0) {
    51
    52 // Project back from homogeneous to camera space
    53 hitPixel = permute ? P.yx : P;
    54
    55 // The depth range that the ray covers within this loop iteration.
    56 // Assume that the ray is moving in increasing z and swap if backwards.
    57 float rayZMin = prevZMaxEstimate;
    58 // Compute the value at 1/2 pixel into the future
    59 float rayZMax = (dQ.z * 0.5 + Q.z) / (dk * 0.5 + k);
    60 prevZMaxEstimate = rayZMax;
    61 if (rayZMin > rayZMax) { swap(rayZMin, rayZMax); }
    62
    63 // Camera-space z of the background at each layer (there can be up to 4)
    64 vec4 sceneZMax = texelFetch(csZBuffer, int2(hitPixel), 0);
    65
    66 if (csZBufferIsHyperbolic) {
        67 # for (int layer = 0; layer < numLayers; ++layer)
        68 sceneZMax[layer] = reconstructCSZ(sceneZMax[layer], clipInfo);
        69 # endfor
        70 }
    71 float4 sceneZMin = sceneZMax - zThickness;
    72
    73 # for (int L = 0; L < numLayers; ++L)
    74 if (((rayZMax >= sceneZMin[L]) && (rayZMin <= sceneZMax[L])) ||
    75 (sceneZMax[L] == 0)) {
        76 hitLayer = layer;
        77 break; // Breaks out of both loops, since the inner loop is a macro
        78 }
    79 # endfor // layer
    80 } // for each pixel on ray
81
82 // Advance Q based on the number of steps
83 Q.xy += dQ.xy * stepCount; hitPoint = Q * (1.0 / k);
84 return all(lessThanEqual(abs(hitPixel - (csZBufferSize * 0.5)),
85 csZBufferSize * 0.5));
86 }
*/


#endif