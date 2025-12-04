#ifndef __CAMERADEBUGGERPASS__HLSL__
#define __CAMERADEBUGGERPASS__HLSL__

#include "../ShaderLibrary/ForwardPlus.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Debug.hlsl"

float _DebugAlpha;

float4 CameraDebuggerPassFragment(Varyings input) : SV_TARGET0
{
    ForwardPlusTile tile = GetForwardPlusTile(input.uv.xy);
	float3 color = tile.IsMinimumEdgePixel(input.uv.xy) ? 1.0 : 0.0;
    if(tile.IsMinimumEdgePixel(input.uv.xy)){
        color = 1;
    }
    else{
        color = OverlayHeatMap(
			input.uv * GetCameraTexSize(), tile.GetScreenSize(),
			tile.GetLightCount(), tile.GetMaxLightsPerTile(), 1.0).rgb;
    }
    return float4(color, _DebugAlpha);
}

#endif