#ifndef __METAPASS__HLSL__
#define __METAPASS__HLSL__

#include "../ShaderLibrary/Surface.hlsl"
#include "../ShaderLibrary/Shadows.hlsl"
#include "../ShaderLibrary/Light.hlsl"
#include "../ShaderLibrary/BRDF.hlsl"

struct Attributes 
{
    float3 posOS : POSITION;
    float2 uv : TEXCOORD0;
    float2 lightMapUV : TEXCOORD1;
};

struct Varyings
{
    float4 posHS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

Varyings MetaPassVertex(Attributes i)
{
    Varyings o;

    i.posOS.xy = i.lightMapUV * unity_LightmapST.xy + unity_LightmapST.zw;
	i.posOS.z = i.posOS.z > 0.0 ? FLT_MIN : 0.0;
    o.posHS = TransformWorldToHClip(i.posOS);
    o.uv = TransformBaseUV(i.uv);

    return o;
}

float4 MetaPassFragment (Varyings input) : SV_TARGET {
    float4 base = GetBase(input.uv);
    Surface surface = (Surface)0;
    surface.color = base.rgb;
    surface.metallic = GetMetallic(input.uv);
    surface.smoothness = GetSmoothness(input.uv);
    BRDF brdf = GetBRDF(surface);
    
    float4 meta = 0.0;
    if (unity_MetaFragmentControl.x) {
        meta = float4(brdf.diffuse, 1.0);
        meta.rgb += brdf.specular * brdf.roughness * 0.5;
        meta.rgb = min(
            PositivePow(meta.rgb, unity_OneOverOutputBoost), unity_MaxOutputValue
        );
    }
    else if (unity_MetaFragmentControl.y) { // 烘培散射光
        meta = float4(GetEmission(input.uv), 1.0);
    }

    return meta;
}

#endif