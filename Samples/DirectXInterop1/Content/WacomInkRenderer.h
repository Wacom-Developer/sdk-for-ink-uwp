#pragma once

#include "..\Common\DeviceResources.h"
#include "ShaderStructures.h"


namespace DirectXInterop1
{
	class WacomInkRenderer
	{
	private:
		Concurrency::critical_section m_CriticalSection;

		Windows::UI::Input::PointerPoint^ m_LastPoint;
		Windows::UI::Input::PointerPoint^ m_LastAddedPoint;

		// Wacom Ink
		Wacom::Ink::RenderingContext^ m_RenderingContext;
		Wacom::Ink::StrokeRenderer^ m_StrokeRenderer;
		Wacom::Ink::Layer^ m_StrokeLayer;
		Wacom::Ink::Layer^ m_RendererLayer;
		Wacom::Ink::Layer^ m_RendererPrelimLayer;
		Wacom::Ink::Path^ m_Path;
		Wacom::Ink::SpeedPathBuilder^ m_PathBuilder;
		Wacom::Ink::Smoothing::MultiChannelSmoothener^ m_Smoothener;

		Windows::UI::Color m_BackgroundColor;
		int m_UpdateFromIndex;
		bool m_PathFinished;
		bool m_ClearStrokeLayer;
		unsigned int m_PointerId;
		bool m_IsPointerIdValid;

		Microsoft::WRL::ComPtr<ID3D11Texture2D> m_OutputTexture;

		// Cached pointer to device resources.
		std::shared_ptr<DX::DeviceResources> m_DeviceResources;

		// Direct3D resources for rect geometry.
		Microsoft::WRL::ComPtr<ID3D11InputLayout>	m_InputLayout;
		Microsoft::WRL::ComPtr<ID3D11Buffer>		m_VertexBuffer;
		Microsoft::WRL::ComPtr<ID3D11Buffer>		m_IndexBuffer;
		Microsoft::WRL::ComPtr<ID3D11VertexShader>	m_VertexShader;
		Microsoft::WRL::ComPtr<ID3D11PixelShader>	m_PixelShader;
		Microsoft::WRL::ComPtr<ID3D11Buffer>		m_ConstantBuffer;
		Microsoft::WRL::ComPtr<ID3D11SamplerState>	m_SamplerState;
		Microsoft::WRL::ComPtr<ID3D11DepthStencilState>		m_DepthDisabledStencilState;
		Microsoft::WRL::ComPtr<ID3D11RasterizerState>		m_RasterizerStateNoCulling;
		Microsoft::WRL::ComPtr<ID3D11BlendState>			m_BlendStateNormal;
		Microsoft::WRL::ComPtr<ID3D11BlendState>			m_BlendStateNone;

		// System resources for rect geometry.
		uint32 m_RectIndicesCount;

		// Variables used with the rendering loop.
		bool m_LoadingComplete;

	public:
		Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> m_ShaderResourceView;
		void EnableAlphaBlending(bool enable);

	private:
		void CreateRasterizerState();
		void InitializeEnabledBlendState(ID3D11BlendState **blendState);
		void InitializeDisabledBlendState(ID3D11BlendState **blendState);
		void AddCurrentPointToPathBuilder(Wacom::Ink::InputPhase phase, Windows::UI::Input::PointerPoint^ p);

	public:
		WacomInkRenderer(const std::shared_ptr<DX::DeviceResources>& deviceResources);

		void CreateDeviceDependentResources();
		void ReleaseDeviceDependentResources();
		void CreateWindowSizeDependentResources();

		void InitInking();
		void RenderInk();
		void Render();
		void CreateSampler();

		void StartTracking(Windows::UI::Input::PointerPoint^ p);
		void StopTracking(Windows::UI::Input::PointerPoint^ p);
	};
}