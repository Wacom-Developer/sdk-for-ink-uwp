using Wacom.Ink;
using Wacom.Ink.Serialization;
using Windows.UI;

namespace Tutorial_02
{
	internal class Stroke
	{
		#region Constructors

		public Stroke(StrokeData strokeData)
		{
			Path = strokeData.Path;
			Width = strokeData.Width;
			Color = strokeData.Color;
			Ts = strokeData.Ts;
			Tf = strokeData.Tf;
		}
		public Stroke(Path srcPath, Color color)
		{
			Path = srcPath.ClonePath();

			Width = null;
			Color = color;
			Ts = 0.0f;
			Tf = 1.0f;
		}

		#endregion

		#region Properties

		public Path Path { get; private set; }
		public float? Width { get; set; }
		public Color Color { get; set; }
		public float Ts { get; set; }
		public float Tf { get; set; }

		#endregion
	}
}
