
Texture2D texture2d : register(t0);
sampler textureSampler : register(s0);


struct PixelShaderInput
{
	float4 pos : SV_POSITION;
	float2 tex : TEXCOORD0;
};


float4 main(PixelShaderInput input) : SV_TARGET
{
	return texture2d.Sample(textureSampler, input.tex);
}
