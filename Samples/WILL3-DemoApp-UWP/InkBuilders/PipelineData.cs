using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wacom.Ink.Geometry;

namespace Wacom
{
	public class PipelineData
	{
		public ProcessorResult<Spline> Spline { get; private set; }
		public ProcessorResult<List<List<Vector2>>> BrushPolys { get; private set; }
		public ProcessorResult<List<List<Vector2>>> Merged { get; private set; }
		public ProcessorResult<List<List<Vector2>>> Simplified { get; private set; }

		public PipelineData(ProcessorResult<Spline> currentSpline, ProcessorResult<List<List<Vector2>>> currentSimplified)
		{
			Spline = currentSpline;
			Simplified = currentSimplified;
		}

		public PipelineData(ProcessorResult<List<List<Vector2>>> currentBrushes, 
			ProcessorResult<List<List<Vector2>>> merged)
		{
			BrushPolys = currentBrushes;
			Merged = merged;
		}
	}
}
