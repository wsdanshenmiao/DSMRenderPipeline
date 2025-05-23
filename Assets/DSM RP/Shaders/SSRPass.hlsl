#ifndef __SSRPASS_HLSL__
#define __SSRPASS_HLSL__

#include "../ShaderLibrary/Common.hlsl"

TEXTURE2D(_NormalTexture);
SAMPLER(sampler_NormalTexture);

CBUFFER_START(_SSRCONSTANTS)
    int _RayMarchingMaxDistance;
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
float4 ViewSpaceRayMarching(Ray ray)
{
    float4 outCol = float4(0, 0, 0, 1);
    float3 endPos = ray.origin;
    [branch]
    if (_RayMarchingStep <= 0 || _RayMarchingMaxDistance <= 0) return outCol;
    if (!RayMarching(ray, _RayMarchingMaxDistance / _RayMarchingStep, _RayMarchingStep, outCol, endPos)) {  // 未找到则进行二分查找
        ray.origin = endPos;
        outCol = BinarySearch(ray);
    }

    return outCol;
}


// 屏幕空间的 RayMarching，输入为视图空间的反射光线
float4 ScreenSpaceRayMarching(Ray ray)
{
    [branch]
    if (_RayMarchingStep <= 0 || _RayMarchingMaxDistance <= 0) return float4(0, 0, 0, 1);
    const int marchingCount = _RayMarchingMaxDistance / _RayMarchingStep;

    // 限制到近平面内
    const float nearPlaneZ = -0.001;
    float rayLen = (ray.origin.z + ray.rayDir.z * _RayMarchingMaxDistance) >= nearPlaneZ ?
        (nearPlaneZ - ray.origin.z) / ray.rayDir.z : _RayMarchingMaxDistance;
    float3 endPosVS = ray.origin + ray.rayDir * rayLen;

    // 转换到NDC空间 [-1, 1]
    float4 startCS = mul(UNITY_MATRIX_P, float4(ray.origin, 1));
    float4 endCS = mul(UNITY_MATRIX_P, float4(endPosVS, 1));
    const float startK = 1.0 / startCS.w, endK = 1.0 / endCS.w;
    startCS *= startK;
    endCS *= endK;

    endCS += (DistanceSquared(startCS, endCS) < 0.0001f) ? float4(0.01, 0.01, 0, 0) : 0;

    // 变换到屏幕空间
    const float2 WH = float2(GetCameraTexWidth(), GetCameraTexHeight());
    const float2 invWH = 1.0f / WH;
    float2 startSS = float2(startCS.x, startCS.y)  * 0.5 + 0.5;
    float2 endSS = float2(endCS.x, endCS.y)  * 0.5 + 0.5;
    startSS *= WH;
    endSS *= WH;

    // 由于后续需要得知当前点的深度，因此还需要保存视图空间下的坐标
    // 由于屏幕空间的步进和视图空间的步进不是线性关系，因此需要使用齐次坐标下的 W 来进行联系
    float3 startQ = ray.origin * startK;
    float3 endQ = endPosVS * endK;

    float2 offsetSS = endSS - startSS;
    bool steep = abs(offsetSS.y) > abs(offsetSS.x); // 斜率是否大于1
    [flatten]
    if (steep) {  // 若斜率大于1则互换
        offsetSS = offsetSS.yx;
        startSS = startSS.yx;
        endSS = endSS.yx;
        steep = true;
    }

    // 步进的方向                       转换为正
    float stepDir = sign(offsetSS.x), invDx = stepDir / offsetSS.x;
    // 每次步进各个变量的偏移
    float3 offsetQ = (endQ - startQ) * invDx;
    float offsetK = (endK - startK) * invDx;
    offsetSS = float2(stepDir, offsetSS.y * invDx);
    //return float4(offsetSS, 0, 1);

    float2 currSS = startSS;
    float3 currQ = startQ;
    float currK = startK;
    float preZ = ray.origin.z;

    // test
#if 0
    int testStep = 1;
    float2 testPos = currSS + offsetSS * testStep;
    float3 testQ = currQ + offsetQ * testStep;
    float testK = currK + offsetK * testStep;
    float2 testUV = steep ? testPos.yx : testPos.xy;
    testUV *= invWH;
    testUV.y = 1 - testUV.y;
    float testMaxZ = (testQ.z ) / (testK );
    float testMinZ = (testQ.z - offsetK) / (testK - offsetK);
    if (testMinZ > testMaxZ) {
        SwapFloat(testMinZ, testMaxZ);
    }
    bool inRange = all(0 <= testUV && testUV <= 1);
    float testSceneZ = -GetCameraLinearDepth(testUV);
    //return testSceneZ - testMinZ;
    //return testMaxZ - testSceneZ + _HitThreshold;
    //return testMinZ <= testSceneZ && testMaxZ >= testSceneZ - _HitThreshold && inRange;
    //return testQ.z / testK / 100;
    return GetCameraColor(testUV);
#endif
    
    [loop]
    for (int iii = 0; (currSS.x * stepDir < endSS.x * stepDir) && iii < marchingCount; ++iii) {
        currSS += offsetSS, currQ.z += offsetQ.z, currK += offsetK;
        
        float2 uv = steep ? currSS.yx : currSS.xy;
        uv *= invWH;
#if UNITY_UV_STARTS_AT_TOP
        uv.y = 1 - uv.y;    // 需要进行反转
#endif
        float sceneZ = -GetCameraLinearDepth(uv);
        
        float minZ = preZ;
        // 通过 K 获得当前位置的深度
        //float maxZ = (currQ.z + 0.5f * offsetQ.z) / (currK + 0.5f * offsetK);
        float maxZ = currQ.z / currK;
        preZ = maxZ;
        [flatten]
        if (minZ > maxZ){
            SwapFloat(minZ, maxZ);
        }
        bool inRange = all(0 <= uv && uv <= 1);
        [branch]
        if (minZ <= sceneZ && maxZ >= sceneZ - _HitThreshold && inRange) {
            float4 col = GetCameraColor(uv);
            return col;
        }
    }

    return float4(0, 0, 0, 1);
}


float4 SSRPassFragment(Varyings input) : SV_TARGET
{
    // 获取RayMarching所需的信息
    float3 normal = SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, input.uv).xyz;
    [branch]
    if (all(normal == 0)) return float4(0, 0, 0, 1);    // 排除天空盒
    normal = normal * 2 - 1;
    normal = normalize(mul((float3x3)unity_MatrixV, normal));
    float3 posVS = GetViewPosition(input.uv);
    float3 viewDir = normalize(posVS);
    float3 rayDir = normalize(reflect(viewDir, normal));    // 若z大于0则会出问题
    //return rayDir.z > 0;
    
    //if (rayDir.z > 0) rayDir.z *= -1;
    //return float4(rayDir, 1);
    
    float4 baseCol = GetCameraColor(input.uv);
    
    Ray ray;
    ray.rayDir = rayDir;
    ray.origin = posVS;
    float4 reflectCol = ScreenSpaceRayMarching(ray);
    //return baseCol;
    return reflectCol;
    //return baseCol + reflectCol;
    //return lerp(baseCol, reflectCol, reflectCol);
}




#endif