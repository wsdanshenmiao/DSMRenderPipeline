#ifndef __COMMON__HLSL__
#define __COMMON__HLSL__

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "UnityInput.hlsl"

#define UNITY_MATRIX_M unity_ObjectToWorld
#define UNITY_MATRIX_I_M unity_WorldToObject
#define UNITY_MATRIX_V unity_MatrixV
#define UNITY_MATRIX_I_V unity_MatrixInvV
#define UNITY_MATRIX_VP unity_MatrixVP
#define UNITY_PREV_MATRIX_M unity_prev_MatrixM
#define UNITY_PREV_MATRIX_I_M unity_prev_MatrixIM
#define UNITY_MATRIX_P glstate_matrix_projection

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

SAMPLER(sampler_linear_clamp);
SAMPLER(sampler_point_clamp);

TEXTURE2D(_CameraColorTexture);
TEXTURE2D(_CameraDepthTexture);

float Square(float v)
{
    return v * v;
}

// 计算两点之间的平方距离
float DistanceSquared(float3 v0, float3 v1)
{
    return dot(v0 - v1, v0 - v1);
}

float GetCameraDepth(float2 uv)
{
    return SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, sampler_point_clamp, uv);
}

float GetCameraLinearDepth(float2 uv)
{
    return LinearEyeDepth(GetCameraDepth(uv), _ZBufferParams);
}

float4 GetCameraColor(float2 uv)
{
    return SAMPLE_TEXTURE2D(_CameraColorTexture, sampler_linear_clamp, uv);
}

float3 GetWorldPosition(float2 uv)
{
    float4 posCS = float4(uv * 2 - 1, GetCameraDepth(uv), 1);
    #if UNITY_UV_STARTS_AT_TOP
    posCS.y *= -1;
    #endif

    float4 posVS = mul(Inverse(UNITY_MATRIX_P), posCS);
    posVS /= posVS.w;
    float3 posWS = mul(UNITY_MATRIX_I_V, posVS).xyz;
    
    return posWS.xyz;
}

float3 GetViewPosition(float2 uv)
{
    float4 posCS = float4(uv * 2 - 1, GetCameraDepth(uv), 1);
    #if UNITY_UV_STARTS_AT_TOP
    posCS.y *= -1;
    #endif

    float4 posVS = mul(Inverse(UNITY_MATRIX_P), posCS);
    posVS /= posVS.w;

    return posVS.xyz;
}

float GetCameraTexWidth()
{
    return _ScreenParams.x;
}

float GetCameraTexHeight()
{
    return _ScreenParams.y;
}

float GetCameraTexInvWidth()
{
    return _ScreenParams.z - 1;
}

float GetCameraTexInvHeight()
{
    return _ScreenParams.w - 1;
}

float3 GetCameraPos()
{
    return _WorldSpaceCameraPos.xyz;
}

float GetFarPlane()
{
    return _ProjectionParams.z;
}

float GetNearPlane()
{
    return _ProjectionParams.y;
}




#endif