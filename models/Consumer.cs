using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginMatrixCalculation.models
{
	public class Consumer
	{
		public Point Point { get; }
		public ConsumerType ConsumerType { get; }

		public Consumer(Point point, ConsumerType consumerType)
		{
			Point = point;
			ConsumerType = consumerType;
		}
	}
}
