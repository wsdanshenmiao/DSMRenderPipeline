#ifndef __SSRPASS_HLSL__
#define __SSRPASS_HLSL__

#include "../ShaderLibrary/Common.hlsl"

TEXTURE2D(_NormalTexture);
SAMPLER(sampler_NormalTexture);

float4 SSRPassFragment(Varyings input) : SV_TARGET
{
    float depth = GetCameraDepth(input.uv);
    float3 posW = GetWorldPosition(input.uv);
    [branch]
    if (depth < 0.0001) return float4(0,0,0,1);

    // 获取RayMarching所需的信息
    float3 normal = SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, input.uv).xyz;
    float3 viewDir = normalize(posW - _WorldSpaceCameraPos.xyz);
    float3 rayDir = normalize(reflect(viewDir, normal));
    float3 currPos = posW;

    float4 baseColor = SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_linear_clamp, input.uv);
    
    [loop]
    for (int i = 0; i < 400; ++i) {
        currPos += rayDir * 0.05;

        // 变换到NDC空间
        float4 posNDC = mul(UNITY_MATRIX_VP, float4(currPos, 1));
        float currDepth = posNDC.w; // 根据投影矩阵可以得到齐次裁剪空间下的 w 就是视图空间下的深度
        posNDC /= posNDC.w;
        
        // _ProjectionParams 为 1 或 -1
        // 重建 uv 来获取采样点的深度及颜色
        float2 uv = float2(posNDC.x, posNDC.y * _ProjectionParams.x) * 0.5 + 0.5;
        // 当前像素的物体的深度
        float linearDepth = GetCameraLinearDepth(uv);

        [branch]
        if (linearDepth < currDepth && currDepth < linearDepth + 0.8) {  // 射线已经穿过了物体
            float4 refCol = GetCameraColor(uv);
            float blend = 0.5;
            return refCol;
            return baseColor * blend + refCol * (1 - blend);
        }
    }

    return float4(0.0f, 0.0f, 0.0f, 1.0f);
    return baseColor;
}


#endif