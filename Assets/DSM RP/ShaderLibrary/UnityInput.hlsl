#ifndef __UNITYINPUT__HLSL__
#define __UNITYINPUT__HLSL__

CBUFFER_START(UnityPerDraw)
    float4x4 unity_ObjectToWorld;
    float4x4 unity_WorldToObject;
    float4 unity_LODFade;
    real4 unity_WorldTransformParams;

    // lightMap的纹理坐标变换
    float4 unity_LightmapST;
    float4 unity_DynamicLightmapST;

    // 球谐函数的各个分量
    float4 unity_SHAr;
    float4 unity_SHAg;
    float4 unity_SHAb;
    float4 unity_SHBr;
    float4 unity_SHBg;
    float4 unity_SHBb;
    float4 unity_SHC;

    // 探针体积
    float4 unity_ProbeVolumeParams;
    float4x4 unity_ProbeVolumeWorldToObject;
    float4 unity_ProbeVolumeSizeInv;
    float4 unity_ProbeVolumeMin;

    bool4 unity_MetaFragmentControl;
    float unity_OneOverOutputBoost;
    float unity_MaxOutputValue;
CBUFFER_END

float4x4 unity_MatrixVP;
float4x4 unity_MatrixV;
float4x4 unity_MatrixInvV;
float4x4 unity_prev_MatrixM;
float4x4 unity_prev_MatrixIM;
float4x4 glstate_matrix_projection;

float3 _WorldSpaceCameraPos;


float4 unity_OrthoParams;
float4 _ProjectionParams;
float4 _ScreenParams;
float4 _ZBufferParams;


#endif