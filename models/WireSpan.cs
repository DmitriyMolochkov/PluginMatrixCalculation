using PluginMatrixCalculation.models.Wires;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginMatrixCalculation.models
{
	public class WireSpan
	{
		public СИП_2 Wire { get; }
		public Point Start { get; }
		public Point End { get; }
		public int Length { get; }

		public WireSpan(Point start, Point end, СИП_2 wire, int length)
		{
			Start = start;
			End = end;
			Wire = wire;
			Length = length;
		}
	}
}
