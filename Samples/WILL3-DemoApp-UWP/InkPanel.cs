using System;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
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

        InkPanelModel Model { get; } = new InkPanelModel();
        InkPanelView View { get; } = new InkPanelView();
        InkPanelController Controller { get; } = null;

		#endregion

		#region Public Interface

		public int StrokesCount => Model.Strokes.Count;

		public void Initialize(SwapChainPanel swapChain)
        {
            View.Graphics.GraphicsReady += Controller.LoadRasterToolsTextures;
            View.InitializeGraphics(swapChain);
            Controller.InitializeMainLoop();
        }

        public void Clear()
        {
            _ = View.Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, () => Controller.Clear());
        }

        public void Dispose()
        {
            Controller.Dispose();
        }

        public void SetOperationMode(OperationMode mode)
        {
            Controller.OperationMode = mode;
        }

        public void SetInkColor(Color color)
        {
			Controller.DrawVectorStrokeOp.Color = color;
			Controller.DrawRasterStrokeOp.Color = color;
		}

        public void LoadStrokesFromModel(Wacom.Ink.Serialization.Model.InkModel inkModel)
        {
            _ = View.Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                Controller.Clear();

                await Model.LoadStrokesFromModel(inkModel, View.Graphics);

				Model.RebuildStrokesCache();

				View.TriggerRedrawSceneAndOverlay();
			});
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

		internal async Task<Wacom.Ink.Serialization.Model.InkModel> CreateUniversalInkModelAsync()
		{
			var inkModel = Model.BuildUniversalInkModelFromCanvasStrokes();

			await Model.BuildUIMBrushesFromAppBrushes(inkModel);

            return inkModel;
		}

		#endregion
	}
}
