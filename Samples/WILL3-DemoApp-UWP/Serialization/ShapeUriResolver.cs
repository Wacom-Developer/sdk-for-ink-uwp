using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wacom.Ink.Geometry;
using Wacom.Ink.Serialization.Model.Enums;

namespace Wacom
{
	public class ShapeUriResolver
	{
		public static List<Vector2> ResolveShape(string shapeUri)
		{
			var uriSplit = shapeUri.Split('?');
			var type = uriSplit[0];
			var arguments = uriSplit[1].Split('&');
			
			if (type == CommonShapeURIs.Ellipse.Uri)
			{
				//E.g. will://brush/3.0/shape/Ellipse?precision=20&radiusX=1.0&radiusY=1.0

				int precision = 20;
				float radiusX = 1.0f;
				float radiusY = 0.5f;

				foreach (var arg in arguments)
				{
					string[] split = arg.Split('='); 

					if (split[0] == "precision")
					{
						bool res = int.TryParse(split[1], out int value);
						if (res)
						{
							precision = value;
						}
					}
					else if (split[0] == "radiusX")
					{
						bool res = float.TryParse(split[1], out float value);
						if (res)
						{
							radiusX = value;
						}
					}
					else if (split[0] == "radiusY")
					{
						bool res = float.TryParse(split[1], out float value);
						if (res)
						{
							radiusY = value;
						}
					}
					else
					{
						throw new NotSupportedException($"Argument '{split[0]}' not supported!");
					}
				}

				return VectorBrushFactory.CreateEllipseBrush(precision, radiusX, radiusY);
			}
			else if (type == CommonShapeURIs.Circle.Uri)
			{
				//E.g. will://brush/3.0/shape/Circle?precision=20&radius=1.0

				int precision = 20;
				float radius = 1.0f;
				
				foreach (var arg in arguments)
				{
					string[] split = arg.Split('=');

					if (split[0] == "precision")
					{
						bool res = int.TryParse(split[1], out int value);
						if (res)
						{
							precision = value;
						}
					}
					else if (split[0] == "radius")
					{
						bool res = float.TryParse(split[1], out float value);
						if (res)
						{
							radius = value;
						}
					}
					else
					{
						throw new NotSupportedException($"Argument '{split[0]}' not supported!");
					}
				}

				return VectorBrushFactory.CreateEllipseBrush(precision, radius, radius);
			}
			else
			{
				throw new NotSupportedException($"Unknown shape URI: {type}");
			}
		}
	}
}
