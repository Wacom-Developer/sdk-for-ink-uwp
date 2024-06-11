using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Manipulations;
using Wacom.Ink.Rendering;
using Windows.Foundation;
using Windows.System.Threading;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
	public enum OperationMode
    {
        VectorDrawing,
        RasterDrawing,
        EraseStrokePart,
        EraseWholeStroke,
        SelectStrokePart,
		SelectWholeStroke,
        MoveSelected,
    }

    class InkPanelController
    {
        #region Fields

        private readonly InkPanelView m_view = null;
        private readonly InkPanelModel m_model = null;

        private PointerManager m_pointerManager;

        private OperationMode m_operationMode = OperationMode.VectorDrawing;

		private UserOperation m_currentOperation = null;

		#endregion

		#region Properties

		public IdleOperation IdleOp { get; }
		public DrawVectorStrokeOperation DrawVectorStrokeOp { get; }
        public DrawRasterStrokeOperation DrawRasterStrokeOp { get; }
        public EraseStrokePartOperation EraseStrokePartOp { get; }
        public EraseWholeStrokeOperation EraseWholeStrokeOp { get; }
        public SelectStrokePartOperation SelectStrokePartOp { get; }
        public SelectWholeStrokeOperation SelectWholeStrokeOp { get; }
        public MoveSelectedStrokesOperation MoveSelectedStrokesOp { get; }
		public Selection Selection { get; } = new Selection();

		#endregion

		#region Constructors

		public InkPanelController(InkPanelModel model, InkPanelView view)
        {
            m_model = model;
            m_view = view;

			Selection = new Selection();

			DrawVectorStrokeOp = new DrawVectorStrokeOperation(this);
            DrawRasterStrokeOp = new DrawRasterStrokeOperation(this);
            EraseStrokePartOp = new EraseStrokePartOperation(this);
            EraseWholeStrokeOp = new EraseWholeStrokeOperation(this);
            SelectStrokePartOp = new SelectStrokePartOperation(this);
            SelectWholeStrokeOp = new SelectWholeStrokeOperation(this);
            MoveSelectedStrokesOp = new MoveSelectedStrokesOperation(this);
			IdleOp = new IdleOperation(this);

            m_currentOperation = IdleOp;
        }

		#endregion

		#region Interface

		public UserOperation CurrentOperation
        {
            get => m_currentOperation;
            set => m_currentOperation = value;
		}
        
		public void InitializeMainLoop()
        {
            m_view.InitializeMainLoop(MainLoopWorkItem());
        }

        public void Dispose()
        {
            m_model.Dispose();
            m_view.Dispose();
        }

        public OperationMode OperationMode
        {
            get
            {
                return m_operationMode;
			}
			set
            {
                if (m_operationMode != value)
                {
                    m_operationMode = value;

                    ClearSelection();

					m_view.InvalidateSceneAndOverlay();
                }
            }
        }

        public void MeasureSelectedStrokes()
        {
            Selection.BoundingRect = m_view.MeasureBoundingRect(m_model.Strokes.Where(stroke => Selection.Contains(stroke.Id)));
		}

		public void Clear()
        {
			Selection.Clear();

			m_model.Clear();

            m_currentOperation = IdleOp;

            m_view.InvalidateSceneAndOverlay();
            m_view.TryRedrawAllStrokes(m_model.Strokes);
            m_view.StopProcessingEvents();
        }

		public void ResetOperation()
		{
			m_currentOperation = IdleOp;
		}

		public void ClearSelection()
		{
            if (Selection.Count > 0)
            {
                Selection.Clear();

                m_view.InvalidateSceneAndOverlay();
            }
		}

		#region Model API

        public int ModelFindStrokeIndex(Identifier id)
        {
            return m_model.FindStrokeIndex(id);
        }

        public void ModelRemoveStroke(int strokeIndex)
        {
            m_model.RemoveStroke(strokeIndex);
        }

        public void ModelStoreStroke(Stroke stroke, int atIndex = -1)
        {
            m_model.StoreStroke(stroke, atIndex);

			stroke.RebuildCache();
		}

        public void ModelMoveSelectedStrokes(Vector2 offset)
        {
			Rect bb = Selection.BoundingRect;

			Selection.BoundingRect = new Rect(bb.X + offset.X, bb.Y + offset.Y, bb.Width, bb.Height);

            Matrix3x2 transform = Matrix3x2.CreateTranslation(offset);

			m_model.MoveSelectedStrokes(transform, Selection);
        }

        public EraseStrokePartManipulation CreateEraseStrokePartOperation()
        {
            return m_model.CreateEraseStrokePartOperation();
        }

        public EraseWholeStrokeManipulation CreateEraseWholeStrokeOperation()
        {
            return m_model.CreateEraseWholeStrokeOperation();
        }

        public SelectStrokePartManipulation CreateSelectStrokePartOperation()
        {
            return m_model.CreateSelectStrokePartOperation();
        }

        public SelectWholeStrokeManipulation CreateSelectWholeStrokeOperation()
        {
            return m_model.CreateSelectWholeStrokeOperation();        
        }

        #endregion

        #region View API

        public void ViewCaptureInputPointer()
        {
            m_view.CaptureInputPointer();
        }

        public void ViewReleaseInputPointer()
        {
            m_view.ReleaseInputPointer();
        }

        public void ViewRenderNewVectorStrokeSegment(ProcessorResult<List<List<Vector2>>> rawPolygons, Windows.UI.Color brushColor)
        {
            m_view.RenderNewVectorStrokeSegment(rawPolygons, brushColor);
        }

        public void ViewRenderNewRasterStrokeSegment(ProcessorResult<Path> path, RasterDrawingTool tool, Windows.UI.Color color, ref DrawStrokeResult drawStrokeResult)
        {
            m_view.RenderNewRasterStrokeSegment(path, tool, color, ref drawStrokeResult);
        }

        public void ViewDrawCurrentStrokeLayer()
        {
            m_view.DrawCurrentStrokeLayer();
        }

        public void ViewClearCurrentStrokeLayer()
        {
            m_view.ClearCurrentStrokeLayer();
        }

		public void ViewInvalidateSceneAndOverlay()
        {
            m_view.InvalidateSceneAndOverlay();
        }

        public void ViewInvalidateOverlay()
        {
            m_view.InvalidateOverlay();
        }

        public void ViewZoomAt(Vector2 viewPoint, float scale)
        {
            m_view.ZoomAt(viewPoint, scale);
        }

        public void ViewPan(float dx, float dy)
        {
            m_view.Scroll(dx, dy);
        }

        #endregion

        #endregion

        #region Implementation

        #region Input Handling

        private void OnPointerPressed(object sender, PointerEventArgs args)
        {
            if (!m_pointerManager.OnPressed(args))
            {
                return;
            }

			m_currentOperation = m_currentOperation.DetermineCurrentOperation(args);

            m_currentOperation.OnPointerPressed(args);
        }

        private void OnPointerMoved(object sender, PointerEventArgs args)
        {
            if (!m_pointerManager.OnMoved(args))
            {
                return;
            }

            if (m_currentOperation == null)
            {
                return;
            }

            m_currentOperation.OnPointerMoved(args);
        }

        private void OnPointerReleased(object sender, PointerEventArgs args)
        {
            if (!m_pointerManager.OnReleased(args))
            {
                return;
            }

            m_currentOperation.OnPointerReleased(args);

        }

        #endregion

        private WorkItemHandler MainLoopWorkItem()
        {
            return new WorkItemHandler((IAsyncAction action) =>
            {
                // The CoreIndependentInputSource will raise pointer events for the specified device types on whichever thread it's created on.
                m_view.CreateInputSource(CoreInputDeviceTypes.Mouse |
                    CoreInputDeviceTypes.Touch |
                    CoreInputDeviceTypes.Pen);

                // Register for pointer events, which will be raised on the background thread.
                m_view.AddPointerPressedHandler(OnPointerPressed);
                m_view.AddPointerMovedHandler(OnPointerMoved);
                m_view.AddPointerReleasedHandler(OnPointerReleased);

                // Synchronize frame rendering - start from the first frame
                uint waitResult = m_view.WaitForSwapChain(1000);

                m_view.ClearLayers();
                m_view.InitialPresent();

                // Start rendering loop
                bool presented = true;

                while (action.Status == AsyncStatus.Started)
                {
                    if (presented)
                    {
                        waitResult = m_view.WaitForSwapChain(1000);
                        presented = false;
                    }

                    m_view.ProcessEvents(CoreProcessEventsOption.ProcessOneAndAllPending);

                    m_currentOperation.UpdateView(m_model, m_view);
                    presented = m_view.Present();
                }
            });
        }

        public async void LoadRasterToolsTextures(Graphics sender, object args)
        {
            foreach (var tool in DrawRasterStrokeOp.RasterTools)
            {
                await ((AppRasterBrush)tool.Brush).LoadBrushTexturesAsync(sender);
            }
        }

		#endregion
	}
}
