#ifndef __BLENDPASS_HLSL__
#define __BLENDPASS_HLSL__

TEXTURE2D(_SrcTexture);

float4 BlendPassFragment(Varyings i) :SV_Target
{
    return SAMPLE_TEXTURE2D(_SrcTexture, sampler_linear_clamp, i.uv);
}


#endif