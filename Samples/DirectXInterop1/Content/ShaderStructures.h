#pragma once

namespace DirectXInterop1
{
	// Constant buffer used to send MVP matrices to the vertex shader.
	struct ModelViewProjectionConstantBuffer
	{
		DirectX::XMFLOAT4X4 model;
		DirectX::XMFLOAT4X4 view;
		DirectX::XMFLOAT4X4 projection;
	};

	// Used to send per-vertex data to the vertex shader.
	struct VertexPositionColor
	{
		DirectX::XMFLOAT3 pos;
		DirectX::XMFLOAT3 color;
	};


	struct ConstantBuffer
	{
		DirectX::XMFLOAT4X4 mWorld;
		DirectX::XMFLOAT4X4 mView;
		DirectX::XMFLOAT4X4 mProjection;
		DirectX::XMFLOAT4 vLightDir[2];
		DirectX::XMFLOAT4 vLightColor[2];
		DirectX::XMFLOAT4 vOutputColor;
	};

	struct VertexPosNormTex
	{
		DirectX::XMFLOAT3 Pos;
		DirectX::XMFLOAT3 Norm;
		DirectX::XMFLOAT2 Tex;
	};

	struct VertexPos2Tex
	{
		DirectX::XMFLOAT2 Pos2;
		DirectX::XMFLOAT2 Tex;
	};

}