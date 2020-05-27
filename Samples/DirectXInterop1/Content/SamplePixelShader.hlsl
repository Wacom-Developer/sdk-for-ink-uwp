
Texture2D txDiffuse : register(t0);
SamplerState samLinear : register(s0);


struct PixelShaderInput
{
	float4 pos : SV_POSITION;
	float3 color : COLOR;
	float2 tex : TEXCOORD0;
};


// A pass-through function for the (interpolated) color data.
float4 main(PixelShaderInput input) : SV_TARGET
{
	float4 texColor = txDiffuse.Sample(samLinear, input.tex);

	// blend the texture
	float3 color = input.color * (1 - texColor.a) + texColor.rgb * texColor.a;

	return float4(color, 1);
}

