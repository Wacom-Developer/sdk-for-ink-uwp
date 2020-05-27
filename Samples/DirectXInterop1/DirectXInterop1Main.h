#pragma once

#include "Common\StepTimer.h"
#include "Common\DeviceResources.h"
#include "Content\Sample3DSceneRenderer.h"
#include "Content\SampleFpsTextRenderer.h"
#include "Content\WacomInkRenderer.h"

// Renders Direct2D and 3D content on the screen.
namespace DirectXInterop1
{
	class DirectXInterop1Main;

	ref class InputHandler sealed
	{
	private:
		DirectXInterop1Main& m_Main;

	internal:
		InputHandler(DirectXInterop1Main& main) :
			m_Main(main)
		{
		}

		// Independent input handling functions.
		void OnPointerPressed(Platform::Object^ sender, Windows::UI::Core::PointerEventArgs^ e);
		//void OnPointerMoved(Platform::Object^ sender, Windows::UI::Core::PointerEventArgs^ e);
		void OnPointerReleased(Platform::Object^ sender, Windows::UI::Core::PointerEventArgs^ e);
	};

	class DirectXInterop1Main : public DX::IDeviceNotify
	{
	public:
		DirectXInterop1Main(const std::shared_ptr<DX::DeviceResources>& deviceResources);
		~DirectXInterop1Main();
		void CreateWindowSizeDependentResources();

		void StartRenderLoop();
		void StopRenderLoop();
		Concurrency::critical_section& GetCriticalSection() { return m_criticalSection; }

		// IDeviceNotify
		virtual void OnDeviceLost();
		virtual void OnDeviceRestored();

		void SetSwapChainPanel(Windows::UI::Xaml::Controls::SwapChainPanel^ swapChainPanel)
		{
			m_swapChainPanel = swapChainPanel;
		}

	private:
		void ProcessInput();
		void Update();
		bool Render();

	public:
		std::unique_ptr<WacomInkRenderer> m_inkRenderer;

	private:
		InputHandler^ m_InputHandler;

		// Cached pointer to device resources.
		std::shared_ptr<DX::DeviceResources> m_deviceResources;

		// TODO: Replace with your own content renderers.
		std::unique_ptr<Sample3DSceneRenderer> m_sceneRenderer;
		std::unique_ptr<SampleFpsTextRenderer> m_fpsTextRenderer;

		Windows::Foundation::IAsyncAction^ m_renderLoopWorker;
		Concurrency::critical_section m_criticalSection;

		// Rendering loop timer.
		DX::StepTimer m_timer;

		Windows::UI::Core::CoreIndependentInputSource^ m_coreInput;
		Windows::UI::Xaml::Controls::SwapChainPanel^ m_swapChainPanel;
	};
}