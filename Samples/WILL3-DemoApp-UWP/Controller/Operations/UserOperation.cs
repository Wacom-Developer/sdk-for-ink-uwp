using System;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using Wacom.Ink.Geometry;
using Wacom.Ink;
using Windows.UI.Core;
using Windows.UI;

namespace WacomInkDemoUWP
{
    public enum CancellationReason
    {
        Manual,
        FocusLost
    }

    abstract class UserOperation
    {
        #region Fields

        protected InkPanelController m_controller = null;

        #endregion

        #region Constructors

        public UserOperation(InkPanelController controller)
        {
            m_controller = controller;
        }

        #endregion

        #region Properties

        public Windows.UI.Color Color { get; set; }

        #endregion

        #region Interface

        public virtual void OnPointerPressed(PointerEventArgs args)
        {
        }

        public virtual void OnPointerMoved(PointerEventArgs args)
        {
        }

        public virtual void OnPointerReleased(PointerEventArgs args)
        {
        }

        public virtual void OnCanceled(CancellationReason reason)
        {
        }

        public virtual void UpdateView(InkPanelModel model, InkPanelView view)
        {
            view.TryResize();

			Func<Stroke, bool> filter = m_controller.Selection.GetNotSelectedStrokesFilter();

			view.TryRedrawAllStrokes(model.Strokes, filter);
            view.TryOverlayRedraw(model, m_controller.Selection, Vector2.Zero);
        }

		public virtual UserOperation DetermineCurrentOperation(PointerEventArgs args)
		{
			return m_controller.CurrentOperation;
		}

		public static VectorStroke CreateVectorStroke(Spline spline, Color color, VectorDrawingTool tool)
		{
			return new VectorStroke(
				Identifier.FromNewGuid(),
				spline,
				color,
				tool.Brush,
				tool.ConstSize,
				tool.ConstRotation,
				tool.ScaleX,
				tool.ScaleY,
				tool.OffsetX,
				tool.OffsetY,
				tool.StrokeBlendMode);
		}

		public static RasterStroke CreateRasterStroke(Spline spline, Color color, RasterDrawingTool tool, uint randomSeed)
		{
			return new RasterStroke(
				Identifier.FromNewGuid(),
				spline,
				color,
				tool.Brush,
				tool.ConstSize,
				tool.ConstRotation,
				tool.ScaleX,
				tool.ScaleY,
				tool.OffsetX,
				tool.OffsetY,
				tool.StrokeBlendMode,
				randomSeed);
		}

		#endregion
	}

	class IdleOperation : UserOperation
	{
		#region Constructors

		public IdleOperation(InkPanelController controller) : base(controller)
		{
		}

		#endregion

		public override UserOperation DetermineCurrentOperation(PointerEventArgs args)
		{
			if (args.CurrentPoint.Properties.IsLeftButtonPressed)
			{
				if ((args.KeyModifiers & Windows.System.VirtualKeyModifiers.Control) != 0)
				{
					return m_controller.SelectStrokePartOp;
				}

				if ((args.KeyModifiers & Windows.System.VirtualKeyModifiers.Shift) != 0)
				{
					return m_controller.SelectWholeStrokeOp;
				}

				if (m_controller.OperationMode == OperationMode.VectorDrawing)
				{
					return m_controller.DrawVectorStrokeOp;
				}

				if (m_controller.OperationMode == OperationMode.RasterDrawing)
				{
					return m_controller.DrawRasterStrokeOp;
				}

				if (m_controller.OperationMode == OperationMode.EraseStrokePart)
				{
					return m_controller.EraseStrokePartOp;
				}

				if (m_controller.OperationMode == OperationMode.EraseWholeStroke)
				{
					return m_controller.EraseWholeStrokeOp;
				}

				if (m_controller.OperationMode == OperationMode.SelectStrokePart)
				{
					return m_controller.SelectStrokePartOp;
				}

				if (m_controller.OperationMode == OperationMode.SelectWholeStroke)
				{
					return m_controller.SelectWholeStrokeOp;
				}
			}

			return m_controller.CurrentOperation;
		}
	}
}
