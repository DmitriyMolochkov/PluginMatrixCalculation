using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginMatrixCalculation.models
{
	public class Pole
	{
		public Point Point { get; }
		public string Number { get; }

		public Pole(Point point, string number)
		{
			Point = point;
			Number = number;
		}
	}
}
