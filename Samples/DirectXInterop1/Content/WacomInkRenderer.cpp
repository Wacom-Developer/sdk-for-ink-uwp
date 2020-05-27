#include "pch.h"
#include "wacom.ink.interop.h"
#include "..\Common\DirectXHelper.h"
#include "WacomInkRenderer.h"


using namespace Microsoft::WRL;
using namespace DirectXInterop1;
using namespace DirectX;
using namespace Windows::Foundation;
using namespace Wacom::Ink;
using namespace Wacom::Ink::Smoothing;
using namespace Wacom::Ink::Interop;



HRESULT GetTextureFromLayer(Layer^ layer, ID3D11Texture2D** ppTexture2D)
{
	LayerProxy^ proxy = ref new LayerProxy();
	proxy->LoadLayer(layer);
	IInspectable* inspectableProxy = reinterpret_cast<IInspectable*>(proxy);

	ComPtr<ILayerProxyNative> layerProxyNative;
	HRESULT hr = inspectableProxy->QueryInterface(__uuidof(ILayerProxyNative), (void**)&layerProxyNative);

	if (SUCCEEDED(hr))
	{
		hr = layerProxyNative->GetTexture(ppTexture2D);
	}

	proxy->Reset();

	return hr;
}

/*
HRESULT GetDeviceContextFromGraphics(Graphics^ graphics, ID3D11DeviceContext1** ppDeviceContext)
{
	auto gp = ref new GraphicsProxy();
	gp->LoadGraphics(graphics);
	IInspectable* inspectableGP = (IInspectable*) reinterpret_cast<IInspectable*>(gp);

	ComPtr<IGraphicsProxyNative> graphicsProxyNative;
	HRESULT hr = inspectableGP->QueryInterface(__uuidof(IGraphicsProxyNative), (void**)&graphicsProxyNative);

	if (SUCCEEDED(hr))
	{
		hr = graphicsProxyNative->GetDeviceContext(ppDeviceContext);
	}

	gp->Reset();

	return hr;
}
*/

RenderingContext^ CreateInkRenderingContext(ID3D11DeviceContext1* pDeviceContext)
{
	RenderingContextFactory^ factory = ref new RenderingContextFactory();

	IInspectable* pInspectableFactory = reinterpret_cast<IInspectable*>(factory);

	ComPtr<IRenderingContextFactoryNative> factoryNative;

	HRESULT hr = pInspectableFactory->QueryInterface(__uuidof(IRenderingContextFactoryNative), (void**)&factoryNative);

	if (FAILED(hr))
		return nullptr;

	ComPtr<IInspectable> inspectableRenderingContext = nullptr;

	hr = factoryNative->CreateInstance(pDeviceContext, inspectableRenderingContext.GetAddressOf());

	if (FAILED(hr))
		return nullptr;

	return reinterpret_cast<RenderingContext^>(inspectableRenderingContext.Get());
}

Layer^ CreateLayer(ID3D11Device1* pDevice, float width, float height, float scaleFactor)
{
	LayerFactory^ factory = ref new LayerFactory();

	IInspectable* pInspectableFactory = reinterpret_cast<IInspectable*>(factory);

	ComPtr<ILayerFactoryNative> factoryNative;

	HRESULT hr = pInspectableFactory->QueryInterface(__uuidof(ILayerFactoryNative), (void**)&factoryNative);

	if (FAILED(hr))
		return nullptr;

	ComPtr<IInspectable> inspectableLayer = nullptr;

	hr = factoryNative->CreateInstance1(pDevice, width, height, scaleFactor, inspectableLayer.GetAddressOf());

	if (FAILED(hr))
		return nullptr;

	return reinterpret_cast<Layer^>(inspectableLayer.Get());
}


WacomInkRenderer::WacomInkRenderer(const std::shared_ptr<DX::DeviceResources>& deviceResources) :
	m_LoadingComplete(false),
	m_RectIndicesCount(0),
	m_DeviceResources(deviceResources),
	m_BackgroundColor(Windows::UI::ColorHelper::FromArgb(0, 0, 0, 0)),
	m_UpdateFromIndex(-1),
	m_PathFinished(true),
	m_IsPointerIdValid(false),
	m_PointerId(0xFFFFFFFF),
	m_ClearStrokeLayer(false)
{
	// Create a path builder
	m_PathBuilder = ref new SpeedPathBuilder();
	m_PathBuilder->SetMovementThreshold(0.1f);
	m_PathBuilder->SetNormalizationConfig(100.0f, 4000.0f);
	m_PathBuilder->SetPropertyConfig(PropertyName::Width, 2.0f, 30.0f, nullptr, nullptr, PropertyFunction::Sigmoid, 0.6191646f, false);

	// Create an object that smooths input data
	m_Smoothener = ref new MultiChannelSmoothener(m_PathBuilder->PathStride);

	m_RenderingContext = CreateInkRenderingContext(m_DeviceResources->GetD3DDeviceContext());

	CreateDeviceDependentResources();
	CreateWindowSizeDependentResources();
}

void WacomInkRenderer::CreateWindowSizeDependentResources()
{
	Size logicalSize = m_DeviceResources->GetLogicalSize();

	if ((logicalSize.Width == 0) || (logicalSize.Height == 0))
	{
		logicalSize = { 1, 1 };
	}

	float scaleFactor = m_DeviceResources->GetCompositionScaleX();

	m_StrokeLayer = CreateLayer(m_DeviceResources->GetD3DDevice(), logicalSize.Width, logicalSize.Height, scaleFactor);
	m_RendererLayer = CreateLayer(m_DeviceResources->GetD3DDevice(), logicalSize.Width, logicalSize.Height, scaleFactor);
	m_RendererPrelimLayer = CreateLayer(m_DeviceResources->GetD3DDevice(), logicalSize.Width, logicalSize.Height, scaleFactor);

	DX::ThrowIfFailed(
		GetTextureFromLayer(m_StrokeLayer, m_OutputTexture.GetAddressOf())
		);

	DX::ThrowIfFailed(
		m_DeviceResources->GetD3DDevice()->CreateShaderResourceView(m_OutputTexture.Get(), nullptr, m_ShaderResourceView.GetAddressOf())
		);

	// Create a stroke renderer
	m_StrokeRenderer = ref new StrokeRenderer();
	m_StrokeRenderer->Init(m_RenderingContext, m_RendererLayer, m_RendererPrelimLayer);
	m_StrokeRenderer->Brush = ref new SolidColorBrush();
	m_StrokeRenderer->StrokeWidth = nullptr;
	m_StrokeRenderer->Color = Windows::UI::Colors::Red;
	m_StrokeRenderer->UseVariableAlpha = false;
	m_StrokeRenderer->Ts = 0.0f;
	m_StrokeRenderer->Tf = 1.0f;
}

void WacomInkRenderer::CreateDeviceDependentResources()
{
	// Load shaders asynchronously.
	auto loadVSTask = DX::ReadDataAsync(L"TextureVS.cso");
	auto loadPSTask = DX::ReadDataAsync(L"TexturePS.cso");

	CreateRasterizerState();

	InitializeEnabledBlendState(m_BlendStateNormal.GetAddressOf());
	InitializeDisabledBlendState(m_BlendStateNone.GetAddressOf());

	// After the vertex shader file is loaded, create the shader and input layout.
	auto createVSTask = loadVSTask.then([this](const std::vector<byte>& fileData) {
		DX::ThrowIfFailed(m_DeviceResources->GetD3DDevice()->CreateVertexShader(&fileData[0], fileData.size(), nullptr, &m_VertexShader));

		// Define the input layout
		static const D3D11_INPUT_ELEMENT_DESC vertexDesc[] =
		{
			{ "POSITION", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 0, D3D11_INPUT_PER_VERTEX_DATA, 0 },
			{ "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 8, D3D11_INPUT_PER_VERTEX_DATA, 0 }
		};

		// create the input layout
		DX::ThrowIfFailed(m_DeviceResources->GetD3DDevice()->CreateInputLayout(vertexDesc, ARRAYSIZE(vertexDesc), &fileData[0], fileData.size(), &m_InputLayout));

		D3D11_DEPTH_STENCIL_DESC depthDisabledStencilDesc;
		ZeroMemory(&depthDisabledStencilDesc, sizeof(depthDisabledStencilDesc));

		// Create a depth stencil state which turns off the Z buffer for 2D rendering.
		// The only difference is that DepthEnable is set to false, all other parameters are the same as the other depth stencil state.
		depthDisabledStencilDesc.DepthEnable = false;
		depthDisabledStencilDesc.DepthWriteMask = D3D11_DEPTH_WRITE_MASK_ALL;
		depthDisabledStencilDesc.DepthFunc = D3D11_COMPARISON_LESS;
		depthDisabledStencilDesc.StencilEnable = false;
		depthDisabledStencilDesc.StencilReadMask = 0xFF;
		depthDisabledStencilDesc.StencilWriteMask = 0xFF;
		depthDisabledStencilDesc.FrontFace.StencilFailOp = D3D11_STENCIL_OP_KEEP;
		depthDisabledStencilDesc.FrontFace.StencilDepthFailOp = D3D11_STENCIL_OP_INCR;
		depthDisabledStencilDesc.FrontFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
		depthDisabledStencilDesc.FrontFace.StencilFunc = D3D11_COMPARISON_ALWAYS;
		depthDisabledStencilDesc.BackFace.StencilFailOp = D3D11_STENCIL_OP_KEEP;
		depthDisabledStencilDesc.BackFace.StencilDepthFailOp = D3D11_STENCIL_OP_DECR;
		depthDisabledStencilDesc.BackFace.StencilPassOp = D3D11_STENCIL_OP_KEEP;
		depthDisabledStencilDesc.BackFace.StencilFunc = D3D11_COMPARISON_ALWAYS;

		// Create the state using the device.
		DX::ThrowIfFailed(m_DeviceResources->GetD3DDevice()->CreateDepthStencilState(&depthDisabledStencilDesc, m_DepthDisabledStencilState.GetAddressOf()));

		// Create a texture sampler
		D3D11_SAMPLER_DESC samplerDesc;
		ZeroMemory(&samplerDesc, sizeof(D3D11_SAMPLER_DESC));
		samplerDesc.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
		samplerDesc.AddressU = D3D11_TEXTURE_ADDRESS_WRAP;
		samplerDesc.AddressV = D3D11_TEXTURE_ADDRESS_WRAP;
		samplerDesc.AddressW = D3D11_TEXTURE_ADDRESS_WRAP;
		samplerDesc.ComparisonFunc = D3D11_COMPARISON_NEVER;
		samplerDesc.MinLOD = 0;
		samplerDesc.MaxLOD = D3D11_FLOAT32_MAX;

		// Create the sample state
		DX::ThrowIfFailed(m_DeviceResources->GetD3DDevice()->CreateSamplerState(&samplerDesc, &m_SamplerState));
	});

	// After the pixel shader file is loaded, create the shader and constant buffer.
	auto createPSTask = loadPSTask.then([this](const std::vector<byte>& fileData) {
		DX::ThrowIfFailed(
			m_DeviceResources->GetD3DDevice()->CreatePixelShader(
			&fileData[0],
			fileData.size(),
			nullptr,
			&m_PixelShader
			)
			);
	});

	// Once both shaders are loaded, create the mesh.
	auto createCubeTask = (createPSTask && createVSTask).then([this]() {

		// Load mesh vertices. Each vertex has a position and a normal.
		static const VertexPos2Tex rectVertices[] =
		{
			{ XMFLOAT2(-1.0f, 1.0f), XMFLOAT2(0.0f, 0.0f) },
			{ XMFLOAT2(1.0f, 1.0f), XMFLOAT2(1.0f, 0.0f) },
			{ XMFLOAT2(-1.0f, -1.0f), XMFLOAT2(0.0f, 1.0f) },
			{ XMFLOAT2(1.0f, -1.0f), XMFLOAT2(1.0f, 1.0f) },
		};

		D3D11_SUBRESOURCE_DATA vertexBufferData = { 0 };
		vertexBufferData.pSysMem = rectVertices;
		vertexBufferData.SysMemPitch = 0;
		vertexBufferData.SysMemSlicePitch = 0;

		CD3D11_BUFFER_DESC vertexBufferDesc(sizeof(rectVertices), D3D11_BIND_VERTEX_BUFFER);

		DX::ThrowIfFailed(
			m_DeviceResources->GetD3DDevice()->CreateBuffer(&vertexBufferDesc, &vertexBufferData, &m_VertexBuffer)
			);

		// Load mesh indices. Each trio of indices represents a triangle to be rendered on the screen.
		static const unsigned short rectIndices[] =
		{
			0, 1, 2, 3
		};

		m_RectIndicesCount = ARRAYSIZE(rectIndices);

		D3D11_SUBRESOURCE_DATA indexBufferData = { 0 };
		indexBufferData.pSysMem = rectIndices;
		indexBufferData.SysMemPitch = 0;
		indexBufferData.SysMemSlicePitch = 0;

		CD3D11_BUFFER_DESC indexBufferDesc(sizeof(rectIndices), D3D11_BIND_INDEX_BUFFER);

		DX::ThrowIfFailed(
			m_DeviceResources->GetD3DDevice()->CreateBuffer(&indexBufferDesc, &indexBufferData, &m_IndexBuffer)
			);
	});

	// Once the cube is loaded, the object is ready to be rendered.
	createCubeTask.then([this]() {

		m_LoadingComplete = true;
	});
}

void WacomInkRenderer::ReleaseDeviceDependentResources()
{
	m_LoadingComplete = false;
	m_VertexShader.Reset();
	m_InputLayout.Reset();
	m_PixelShader.Reset();
	m_ConstantBuffer.Reset();
	m_VertexBuffer.Reset();
	m_IndexBuffer.Reset();

	m_OutputTexture.Reset();
	m_SamplerState.Reset();
	m_DepthDisabledStencilState.Reset();
	m_RasterizerStateNoCulling.Reset();
	m_BlendStateNormal.Reset();
	m_BlendStateNone.Reset();
}

void WacomInkRenderer::CreateRasterizerState()
{
	D3D11_RASTERIZER_DESC rDesc;
	ZeroMemory(&rDesc, sizeof(rDesc));
	rDesc.AntialiasedLineEnable = false;
	rDesc.CullMode = D3D11_CULL_MODE::D3D11_CULL_NONE;
	rDesc.DepthBias = 0;
	rDesc.DepthBiasClamp = 0.0f;
	rDesc.DepthClipEnable = false;
	rDesc.FillMode = D3D11_FILL_MODE::D3D11_FILL_SOLID;
	rDesc.FrontCounterClockwise = false;
	rDesc.MultisampleEnable = false;
	rDesc.ScissorEnable = false;
	rDesc.SlopeScaledDepthBias = 0.0f;

	DX::ThrowIfFailed(
		m_DeviceResources->GetD3DDevice()->CreateRasterizerState(&rDesc, m_RasterizerStateNoCulling.GetAddressOf())
		);
}

void WacomInkRenderer::InitializeEnabledBlendState(ID3D11BlendState **blendState)
{
	const D3D11_BLEND srcBlend = D3D11_BLEND_ONE;
	const D3D11_BLEND srcBlendAlpha = D3D11_BLEND_ONE;
	const D3D11_BLEND destBlend = D3D11_BLEND_INV_SRC_ALPHA;
	const D3D11_BLEND destBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
	const D3D11_BLEND_OP blendOp = D3D11_BLEND_OP_ADD;
	const D3D11_BLEND_OP blendOpAlpha = D3D11_BLEND_OP_ADD;

	D3D11_BLEND_DESC blendStateDesc;
	ZeroMemory(&blendStateDesc, sizeof(D3D11_BLEND_DESC));
	blendStateDesc.RenderTarget[0].BlendEnable = TRUE;
	blendStateDesc.RenderTarget[0].SrcBlend = srcBlend;
	blendStateDesc.RenderTarget[0].DestBlend = destBlend;
	blendStateDesc.RenderTarget[0].BlendOp = blendOp;
	blendStateDesc.RenderTarget[0].SrcBlendAlpha = srcBlendAlpha;
	blendStateDesc.RenderTarget[0].DestBlendAlpha = destBlendAlpha;
	blendStateDesc.RenderTarget[0].BlendOpAlpha = blendOpAlpha;
	blendStateDesc.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;

	DX::ThrowIfFailed(m_DeviceResources->GetD3DDevice()->CreateBlendState(&blendStateDesc, blendState));
}

void WacomInkRenderer::InitializeDisabledBlendState(ID3D11BlendState **blendState)
{
	D3D11_BLEND_DESC blendStateDesc;
	ZeroMemory(&blendStateDesc, sizeof(D3D11_BLEND_DESC));
	blendStateDesc.RenderTarget[0].BlendEnable = FALSE;
	blendStateDesc.RenderTarget[0].SrcBlend = D3D11_BLEND_ONE;
	blendStateDesc.RenderTarget[0].DestBlend = D3D11_BLEND_ZERO;
	blendStateDesc.RenderTarget[0].BlendOp = D3D11_BLEND_OP_ADD;
	blendStateDesc.RenderTarget[0].SrcBlendAlpha = D3D11_BLEND_ONE;
	blendStateDesc.RenderTarget[0].DestBlendAlpha = D3D11_BLEND_ZERO;
	blendStateDesc.RenderTarget[0].BlendOpAlpha = D3D11_BLEND_OP_ADD;
	blendStateDesc.RenderTarget[0].RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;

	DX::ThrowIfFailed(m_DeviceResources->GetD3DDevice()->CreateBlendState(&blendStateDesc, blendState));
}

void WacomInkRenderer::EnableAlphaBlending(bool enable)
{
	float blendFactor[4] = { 0.0f, 0.0f, 0.0f, 0.0f };
	UINT sampleMask = 0xffffffff;

	if (enable)
	{
		m_DeviceResources->GetD3DDeviceContext()->OMSetBlendState(m_BlendStateNormal.Get(), blendFactor, sampleMask);
	}
	else
	{
		m_DeviceResources->GetD3DDeviceContext()->OMSetBlendState(m_BlendStateNone.Get(), blendFactor, sampleMask);
	}
}

void WacomInkRenderer::RenderInk()
{
	// Loading is asynchronous. Only draw geometry after it's loaded.
	if (!m_LoadingComplete)
	{
		return;
	}

	Concurrency::critical_section::scoped_lock lock(m_CriticalSection);

	if (m_IsPointerIdValid)
	{
		Windows::UI::Input::PointerPoint^ pp = Windows::UI::Input::PointerPoint::GetCurrentPoint(m_PointerId);

		AddCurrentPointToPathBuilder(InputPhase::Move, pp);
	}

	if (m_ClearStrokeLayer)
	{
		// Reset the stroke renderer
		m_StrokeRenderer->ResetAndClear();

		// Clear the stroke layer
		m_RenderingContext->SetTarget(m_StrokeLayer);
		m_RenderingContext->ClearColor(m_BackgroundColor);
		m_ClearStrokeLayer = false;
	}

	if (m_UpdateFromIndex < 0)
		return;

	Path^ currentPath = m_PathBuilder->CurrentPath;

	int numberOfPointsToDraw = currentPath->PointsCount - m_UpdateFromIndex;
	if (numberOfPointsToDraw <= 0)
		return;

	m_StrokeRenderer->DrawStroke(currentPath, m_UpdateFromIndex, numberOfPointsToDraw, m_PathFinished);

	// reset the starting index
	m_UpdateFromIndex = -1;

	// draw preliminary path
	if (!m_PathFinished)
	{
		Path^ prelimPathPart = m_PathBuilder->CreatePreliminaryPath();

		if (prelimPathPart->PointsCount > 0)
		{
			m_Smoothener->Smooth(prelimPathPart, true);

			Path^ preliminaryPath = m_PathBuilder->FinishPreliminaryPath(prelimPathPart);

			m_StrokeRenderer->DrawPreliminaryStroke(preliminaryPath, 0, preliminaryPath->PointsCount);
		}
	}

	// recompose the scene within the updated area
	m_RenderingContext->SetTarget(m_StrokeLayer, m_StrokeRenderer->UpdatedRect);
	m_RenderingContext->ClearColor(m_BackgroundColor);

	// draw
	m_StrokeRenderer->BlendStrokeUpdatedAreaInLayer(m_StrokeLayer, BlendMode::Normal);
	m_RenderingContext->SetTarget(nullptr);
}

void WacomInkRenderer::Render()
{
	// Loading is asynchronous. Only draw geometry after it's loaded.
	if (!m_LoadingComplete)
	{
		return;
	}

	auto context = m_DeviceResources->GetD3DDeviceContext();

	// set the rasterizer state
	context->RSSetState(m_RasterizerStateNoCulling.Get());

	// enable alpha blending
	EnableAlphaBlending(true);

	context->OMSetDepthStencilState(m_DepthDisabledStencilState.Get(), 1);

	UINT stride = sizeof(VertexPos2Tex);
	UINT offset = 0;
	context->IASetInputLayout(m_InputLayout.Get());
	context->IASetVertexBuffers(0, 1, m_VertexBuffer.GetAddressOf(), &stride, &offset);
	context->IASetIndexBuffer(m_IndexBuffer.Get(), DXGI_FORMAT_R16_UINT, 0);
	context->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLESTRIP);

	context->VSSetShader(m_VertexShader.Get(), nullptr, 0);

	context->PSSetShader(m_PixelShader.Get(), nullptr, 0);
	context->PSSetShaderResources(0, 1, m_ShaderResourceView.GetAddressOf());
	context->PSSetSamplers(0, 1, m_SamplerState.GetAddressOf());

	// draw a rect filled with the output layer as a texture
	context->DrawIndexed(m_RectIndicesCount, 0, 0);

	// detach the ink texture
	ID3D11ShaderResourceView* pNull = nullptr;
	context->PSSetShaderResources(0, 1, &pNull);

	// disable alpha blending
	EnableAlphaBlending(false);
}

void WacomInkRenderer::StartTracking(Windows::UI::Input::PointerPoint^ p)
{
	if (!m_LoadingComplete)
		return;

	// If currently there is an unfinished stroke - do not interrupt it
	if (m_IsPointerIdValid)
		return;

	m_LastPoint = p;

	Concurrency::critical_section::scoped_lock lock(m_CriticalSection);

	// Capture the pointer and store its Id
	m_PointerId = p->PointerId;
	m_IsPointerIdValid = true;

	// Reset the state related to path building
	m_UpdateFromIndex = -1;
	m_PathFinished = false;

	// Reset the smoothener
	m_Smoothener->Reset();

	m_ClearStrokeLayer = true;

	// Add the pointer point to the path builder
	AddCurrentPointToPathBuilder(InputPhase::Begin, p);
	m_LastAddedPoint = p;
}

void WacomInkRenderer::StopTracking(Windows::UI::Input::PointerPoint^ p)
{
	if (!m_LoadingComplete)
		return;

	// Ignore events from other pointers
	if (!m_IsPointerIdValid || (p->PointerId != m_PointerId))
		return;

	Concurrency::critical_section::scoped_lock lock(m_CriticalSection);

	// Reset the stored id and release the pointer capture
	m_IsPointerIdValid = false;
	m_PointerId = 0xFFFFFFFF;

	AddCurrentPointToPathBuilder(InputPhase::End, p);
	m_LastAddedPoint = p;

	m_PathFinished = true;
}

void WacomInkRenderer::AddCurrentPointToPathBuilder(InputPhase phase, Windows::UI::Input::PointerPoint^ p)
{
	Path^ pathPart = m_PathBuilder->AddPoint(phase, p);

	if (pathPart->PointsCount > 0)
	{
		m_Smoothener->Smooth(pathPart, phase == InputPhase::End);

		int indexOfFirstAffectedPoint;
		m_PathBuilder->AddPathPart(pathPart, &indexOfFirstAffectedPoint);

		if (m_UpdateFromIndex == -1)
		{
			m_UpdateFromIndex = indexOfFirstAffectedPoint;
		}
	}
}

