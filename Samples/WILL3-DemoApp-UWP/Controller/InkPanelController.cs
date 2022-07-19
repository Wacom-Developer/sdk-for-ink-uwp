using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Manipulations;
using Wacom.Ink.Rendering;
using Windows.Foundation;
using Windows.System.Threading;
using Windows.UI;
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
        SelectStrokeWhole,
        MoveSelected,
    }

    public class InkPanelController
    {
        #region Fields
        private readonly UserOperation EmptyOp;

        private readonly InkPanelView m_view = null;
        private readonly InkPanelModel m_model = null;

        private PointerManager m_pointerManager;

        private OperationMode m_operationMode = OperationMode.VectorDrawing;
        private UserOperation m_currentOperation = null;

        #endregion

        #region Properties

        public bool UseNewInterpolator { get; } = true;
        public DrawVectorStrokeOperation DrawVectorStrokeOp { get; }
        public DrawRasterStrokeOperation DrawRasterStrokeOp { get; }
        public EraseStrokePartOperation EraseStrokePartOp { get; }
        public EraseWholeStrokeOperation EraseWholeStrokeOp { get; }
        public SelectStrokePartOperation SelectStrokePartOp { get; }
        public SelectWholeStrokeOperation SelectWholeStrokeOp { get; }
        public MoveSelectedStrokesOperation MoveSelectedStrokesOp { get; }

        #endregion


        #region Constructors

        public InkPanelController(InkPanelModel model, InkPanelView view)
        {
            m_model = model;
            m_view = view;

            DrawVectorStrokeOp = new DrawVectorStrokeOperation(this);
            DrawRasterStrokeOp = new DrawRasterStrokeOperation(this);

            EraseStrokePartOp = new EraseStrokePartOperation(this);
            EraseWholeStrokeOp = new EraseWholeStrokeOperation(this);

            SelectStrokePartOp = new SelectStrokePartOperation(this);
            SelectWholeStrokeOp = new SelectWholeStrokeOperation(this);

            MoveSelectedStrokesOp = new MoveSelectedStrokesOperation(this);

            EmptyOp = new UserOperation(this);

            m_currentOperation = EmptyOp;

        }

        #endregion

        #region Interface

        public void InitializeMainLoop()
        {
            m_view.InitializeMainLoop(MainLoopWorkItem());
        }

        public void Dispose()
        {
            m_model.Dispose();
            m_view.Dispose();
        }

        public void SetOperationMode(OperationMode mode)
        {
            if (m_operationMode != mode)
            {
                m_operationMode = mode;
                m_view.TryRedrawAllStrokes(m_model.Strokes);
            }
        }

        public void MeasureSelectedStrokes()
        {
            MoveSelectedStrokesOp.BoundingRect = m_view.MeasureBoundingRect(m_model.Strokes.Where(stroke => m_model.SelectedStrokes.Contains(stroke.Id)));
        }


        public void Clear()
        {
            m_model.ClearState();
            ClearSelection();

            m_view.InvalidateSceneAndOverlay();
            m_view.TryRedrawAllStrokes(m_model.Strokes);//.Where(stroke => !m_model.SelectedStrokes.Contains(stroke.Id)));
            //m_view.TryOverlayRedraw(m_model); // No selected strokes to draw
            m_view.StopProcessingEvents();
        }

        public double RebuildAndRepaintStrokesAndOverlay()
        {
            double milliseconds = m_model.RebuildStrokesCache(UseNewInterpolator);

            if (m_view.IsGraphicsInitialized())
            {
                m_view.TriggerRedrawSceneAndOverlay();
            }

            return milliseconds;
        }

        #region Model API

        public VectorStroke ModelCreateVectorStroke(Spline spline, Color color, VectorDrawingTool tool, string tag = null)
        {
            float viewToModelScale;

            if (m_view.TransformMatrix.IsIdentity)
            {
                viewToModelScale = 1.0f;
            }
            else
            {
                var viewToModelMatrix = m_view.InverseTransformMatrix;

                viewToModelScale = viewToModelMatrix.GetScale();

                Stroke.TransformPath(spline.Path, viewToModelMatrix, viewToModelScale);
            }

            VectorStroke stroke = new VectorStroke(
                Identifier.FromNewGuid(),
                spline,
                color,
                tool.Paint.Brush,
                tool.ConstSize,     // FIX: multiply by viewToModelScale?
                tool.ConstRotation, // FIX: must be rotated if the view is rotated
                tool.Paint.ScaleX,
                tool.Paint.ScaleY,
                tool.Paint.OffsetX * viewToModelScale,
                tool.Paint.OffsetY * viewToModelScale,
                viewToModelScale,
                tool.Paint.StrokeBlendMode,
                0,
                tag);

            stroke.RebuildCache(UseNewInterpolator);

            return stroke;
        }

        public RasterStroke ModelCreateRasterStroke(Spline spline, Color color, RasterDrawingTool tool, uint randomSeed, string tag = null)
        {
            float viewToModelScale;

            if (m_view.TransformMatrix.IsIdentity)
            {
                viewToModelScale = 1.0f;
            }
            else
            {
                var viewToModelMatrix = m_view.InverseTransformMatrix;

                viewToModelScale = viewToModelMatrix.GetScale();

                Stroke.TransformPath(spline.Path, viewToModelMatrix, viewToModelScale);
            }

            RasterStroke stroke = new RasterStroke(
                Identifier.FromNewGuid(),
                spline,
                color,
                tool.Paint.Brush,
                tool.ConstSize,     // FIX: multiply by viewToModelScale?
                tool.ConstRotation, // FIX: must be rotated if the view is rotated
                tool.Paint.ScaleX,
                tool.Paint.ScaleY,
                tool.Paint.OffsetX * viewToModelScale,
                tool.Paint.OffsetY * viewToModelScale,
                viewToModelScale,
                tool.Paint.StrokeBlendMode,
                randomSeed,
                0,
                tag);

            stroke.RebuildCache(UseNewInterpolator);

            return stroke;
        }

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
        }

        public void ModelSelectStroke(Identifier id)
        {
            m_model.SelectedStrokes.Add(id);
        }

        public void ModelMoveSelectedStrokes(Matrix3x2 transform)
        {
            m_model.MoveSelectedStrokes(transform);
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

        public void ModelEnsureStrokesCacheExists()
        {
            m_model.EnsureStrokesCacheExists(UseNewInterpolator);
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

        public Vector2 ViewTransformToModel(Vector2 point)
        {
            return Vector2.Transform(point, m_view.InverseTransformMatrix);
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

            DetermineCurrentOperation(args);

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
            ResetOperation();
        }

        private void OnInkCanvasPointerWheelChanged(object sender, PointerEventArgs args)
        {
            DetermineCurrentOperation(args);
            m_currentOperation.OnPointerWheelChanged(args);
            ResetOperation();
        }

        private void ResetOperation()
        {
            m_currentOperation = EmptyOp;
        }

        private void DetermineCurrentOperation(PointerEventArgs args)
        {
            Debug.WriteLine($"DetermineCurrentOperation opMode={m_operationMode}  currentOp is {m_currentOperation.GetType().Name}");

            if (m_currentOperation != EmptyOp)
            {
                return;
            }

            if (args.CurrentPoint.Properties.IsLeftButtonPressed)
            {
                bool clearSelection = m_model.SelectedStrokes.Any();

                if (clearSelection &&
                    MoveSelectedStrokesOp.BoundingRect.Contains(args.CurrentPoint.Position))
                {
                    // Click inside selection rect 
                    clearSelection = false;
                }

                if (m_operationMode == OperationMode.VectorDrawing)
                {
                    m_currentOperation = DrawVectorStrokeOp;
                }
                else if (m_operationMode == OperationMode.RasterDrawing)
                {
                    m_currentOperation = DrawRasterStrokeOp;
                }
                else if (m_operationMode == OperationMode.EraseStrokePart)
                {
                    m_currentOperation = EraseStrokePartOp;
                }
                else if (m_operationMode == OperationMode.EraseWholeStroke)
                {
                    m_currentOperation = EraseWholeStrokeOp;
                }
                else if (m_operationMode == OperationMode.MoveSelected)
                {
                    if (clearSelection)
                    {
                        SetOperationMode(MoveSelectedStrokesOp.SelectionMode);
                        m_currentOperation = MoveSelectedStrokesOp.SelectionMode == OperationMode.SelectStrokePart ? SelectStrokePartOp : (UserOperation)SelectWholeStrokeOp;
                    }
                    else
                    {
                        // Restore current op to MoveSelected
                        m_currentOperation = MoveSelectedStrokesOp;
                    }
                }
                else if (m_operationMode == OperationMode.SelectStrokePart)
                {
                    if (!SwitchToMoveMode(args.CurrentPoint.Position))
                    {
                        Debug.WriteLine($"  Op => SelectStrokePartOp (no switch to move)");
                        m_currentOperation = SelectStrokePartOp;
                    }
                    else
                    {
                        // Restore current op to MoveSelected
                        m_currentOperation = MoveSelectedStrokesOp;
                    }
                }
                else if (m_operationMode == OperationMode.SelectStrokeWhole)
                {
                    if (!SwitchToMoveMode(args.CurrentPoint.Position))
                    {
                        m_currentOperation = SelectWholeStrokeOp;
                    }
                    else
                    {
                        // Restore current op to SelectStrokeWhole
                        m_currentOperation = MoveSelectedStrokesOp;
                    }
                }
                if (clearSelection)
                {
                    ClearSelection();
                    m_view.InvalidateSceneAndOverlay();
                }
            }
        }

        private void ClearSelection()
        {
            m_model.SelectedStrokes.Clear();
            MoveSelectedStrokesOp.BoundingRect = Rect.Empty;
        }

        private bool SwitchToMoveMode(Point currentPosition)
        {
            if (MoveSelectedStrokesOp.BoundingRect.Contains(currentPosition))
            {
                // Click inside selection rect - switch to move mode
                MoveSelectedStrokesOp.SelectionMode = m_operationMode;
                SetOperationMode(OperationMode.MoveSelected);
                m_currentOperation = MoveSelectedStrokesOp;
                return true;
            }
            return false;
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
                m_view.AddPointerWheelChangedHandler(OnInkCanvasPointerWheelChanged);

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
                    //Windows.UI.Color color = m_currentOperation == null ? Windows.UI.Color.FromArgb(0, 0, 0, 0) : m_currentOperation.Color;
                    //m_view.Update(m_model.Strokes, m_model.CustomShapes, m_inkBuilder, color);
                    m_currentOperation.UpdateView(m_model, m_view);
                    presented = m_view.Present();
                }
            });
        }

        public async void LoadRasterToolsTextures(Graphics sender, object args)
        {
            foreach (var tool in DrawRasterStrokeOp.RasterTools)
            {
                await ((AppRasterBrush)tool.Paint.Brush).LoadBrushTexturesAsync(sender);
            }
        }

        #endregion
    }
}
