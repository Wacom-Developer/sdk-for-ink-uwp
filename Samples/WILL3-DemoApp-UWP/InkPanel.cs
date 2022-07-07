using System;
using System.Threading.Tasks;
using Wacom.Ink.Serialization.Model;
using Windows.UI.Xaml.Controls;

namespace WacomInkDemoUWP
{
    public class InkPanel : IDisposable
    {
        #region Constructors

        public InkPanel()
        {
            Controller = new InkPanelController(Model, View);
        }

        #endregion

        #region Properties

        public InkPanelModel Model { get; } = new InkPanelModel();
        public InkPanelView View { get; } = new InkPanelView();
        public InkPanelController Controller { get; private set; } = null;

        #endregion

        #region Public Interface

        public void Initialize(SwapChainPanel swapChain)
        {
            View.Graphics.GraphicsReady += Controller.LoadRasterToolsTextures;
            View.InitializeGraphics(swapChain);
            Controller.InitializeMainLoop();
        }

        public void Clear()
        {
            Controller.Clear();
        }

        public void Dispose()
        {
            Controller.Dispose();
        }

        public double RebuildAndRepaintStrokesAndOverlay()
        {
            return Controller.RebuildAndRepaintStrokesAndOverlay();
        }

        public async Task LoadStrokesFromModel(InkModel inkModel)
        {
            await Model.LoadStrokesFromModel(inkModel, View.Graphics);

            foreach (var stroke in Model.Strokes)
            {
                stroke.RebuildCache(Controller.UseNewInterpolator);
            }
        }

        public bool SetVectorTool(string toolName)
        {
            VectorDrawingTool tool = Controller.DrawVectorStrokeOp.VectorTools.Find((item) => item.Uri == toolName);

            if (tool == null)
                return false;

            Controller.DrawVectorStrokeOp.Tool = tool;
            return true;
        }

        public bool SetRasterTool(string toolName)
        {
            RasterDrawingTool tool = Controller.DrawRasterStrokeOp.RasterTools.Find((item) => item.Uri == toolName);

            if (tool == null)
                return false;

            Controller.DrawRasterStrokeOp.Tool = tool;
            return true;
        }

        public bool SetPartialStrokeEraserTool(string toolName)
        {
            VectorDrawingTool tool = Controller.EraseStrokePartOp.VectorTools.Find((item) => item.Uri == toolName);

            if (tool == null)
                return false;

            Controller.EraseStrokePartOp.Tool = tool;
            return true;
        }

        public bool SetWholeStrokeEraserTool(string toolName)
        {
            VectorDrawingTool tool = Controller.EraseWholeStrokeOp.VectorTools.Find((item) => item.Uri == toolName);

            if (tool == null)
                return false;

            Controller.EraseWholeStrokeOp.Tool = tool;
            return true;
        }

        public bool SetPartialStrokeSelectorTool(string toolName)
        {
            VectorDrawingTool tool = Controller.SelectStrokePartOp.VectorTools.Find((item) => item.Uri == toolName);

            if (tool == null)
                return false;

            Controller.SelectStrokePartOp.Tool = tool;
            return true;
        }

        public bool SetWholeStrokeSelectorTool(string toolName)
        {
            VectorDrawingTool tool = Controller.SelectWholeStrokeOp.VectorTools.Find((item) => item.Uri == toolName);

            if (tool == null)
                return false;

            Controller.SelectWholeStrokeOp.Tool = tool;
            return true;
        }

        #endregion

    }
}
