using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Wacom
{
	public class VectorBrushFactory
	{
		public static List<Vector2> CreateRectBrush(float width, float height)
		{
			var widthOffset = width / 2;
			var heightOffset = height / 2;

			return new List<Vector2>() { new Vector2(widthOffset, -heightOffset), new Vector2(widthOffset, heightOffset), new Vector2(-widthOffset, heightOffset), new Vector2(-widthOffset, -heightOffset) };
		}

		public static List<Vector2> CreateTrapezoidBrush(float height, float topBase, float bottomBase)
		{
			var mid = height / 2;
			var midTopBase = topBase / 2;
			var midBottomBase = bottomBase / 2;

			return new List<Vector2>() { new Vector2(midTopBase, -mid), new Vector2(midBottomBase, mid), new Vector2(-midBottomBase, mid), new Vector2(-midTopBase, -mid) };
		}

		public static List<Vector2> CreateTriangleBrush(float sideLenght)
		{
			var altitude = Math.Sqrt(3) / 2 * sideLenght;
			var apothem = (float)altitude / 3;
			var halfSide = sideLenght / 2;

			return new List<Vector2>() { new Vector2(0, -(apothem * 2)), new Vector2(halfSide, apothem), new Vector2(-halfSide, apothem) };
		}

		public static List<Vector2> CreateEllipseBrush(int pointsNum, float width, float height)
		{
			List<Vector2> brushPoints = new List<Vector2>();

			double radiansStep = Math.PI * 2 / pointsNum;
			double currentRadian = 0.0;

			for (var i = 0; i < pointsNum; i++)
			{
				currentRadian = i * radiansStep;
				brushPoints.Add(new Vector2((float)(width * Math.Cos(currentRadian)),
											(float)(height * Math.Sin(currentRadian))));
			}

			return brushPoints;
		}

		public static List<Vector2> CreateNgonBrush(int vertexCount, float diameter)
		{
			var radius = diameter / 2;
			return CreateEllipseBrush(vertexCount, radius, radius);
		}
	}
}
