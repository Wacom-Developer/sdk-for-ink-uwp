using System.Linq;
using System.Numerics;
using Windows.Foundation;
using Windows.UI.Core;

namespace WacomInkDemoUWP
{
    public class MoveSelectedStrokesOperation : UserOperation
    {
        private Point       m_origin;
        private Matrix3x2   m_transform;

        public Rect BoundingRect { get; set; } = Rect.Empty;
        public OperationMode SelectionMode { get; set; }
                                                                                
        public MoveSelectedStrokesOperation(InkPanelController controller)
            : base(controller)
        {
        }

        public override void OnPointerPressed(PointerEventArgs args)
        {
            m_origin = args.CurrentPoint.Position;
            m_transform = Matrix3x2.Identity;

            m_controller.ViewInvalidateSceneAndOverlay();
        }

        public override void OnPointerMoved(PointerEventArgs args)
        {
            m_transform = Matrix3x2.CreateTranslation(OffsetFromOrigin(args.CurrentPoint.Position));

        }

        public override void OnPointerReleased(PointerEventArgs args)
        {
            if (BoundingRect != Rect.Empty)
            {
                var offset = OffsetFromOrigin(args.CurrentPoint.Position);

                BoundingRect = new Rect(BoundingRect.X + offset.X, BoundingRect.Y + offset.Y, BoundingRect.Width, BoundingRect.Height);

                m_controller.ModelMoveSelectedStrokes(Matrix3x2.CreateTranslation(offset));
            }
            else
            {
                System.Diagnostics.Debugger.Break();
            }
        }

        public override void UpdateView(InkPanelModel model, InkPanelView view)
        {
            m_controller.ViewInvalidateOverlay();
            view.TryResize();
            view.TryRedrawAllStrokes(model.Strokes.Where(stroke => !model.SelectedStrokes.Contains(stroke.Id)));
            view.TryOverlayRedraw(model, m_transform);
        }

        private Vector2 OffsetFromOrigin(Point p)
        {
            return new Vector2((float)(p.X - m_origin.X), (float)(p.Y - m_origin.Y));
        }

    }
}
