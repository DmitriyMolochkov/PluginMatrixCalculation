using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginMatrixCalculation.models.Wires
{
	public class СИП_2
	{
		public readonly ComplexNumber PhaseWireResistivity;
		public readonly ComplexNumber NeutralWireResistivity;
		public readonly string Name;

		private СИП_2(ComplexNumber phaseWireResistivity, ComplexNumber neutralWireResistivity, string name)
		{
			PhaseWireResistivity = phaseWireResistivity;
			NeutralWireResistivity = neutralWireResistivity;
			Name = name;
		}

		public static readonly СИП_2 _3х35_1х54_1х25 = new СИП_2(
			new ComplexNumber(1.111, 0.0802),
			new ComplexNumber(0.923, 0.0691),
			"СИП-2 3х35+1х54,6+1х25"
			);

		public static readonly СИП_2 _3х50_1х54_1х25 = new СИП_2(
			new ComplexNumber(0.822, 0.0794),
			new ComplexNumber(0.923, 0.0687),
			"СИП-2 3х50+1х54,6+1х25"
			);

		public static readonly СИП_2 _3х70_1х54_1х25 = new СИП_2(
			new ComplexNumber(0.568, 0.0785),
			new ComplexNumber(0.923, 0.0679),
			"СИП-2 3х70+1х54,6+1х25"
			);

		public static readonly СИП_2 _3х95_1х70_1х25 = new СИП_2(
			new ComplexNumber(0.411, 0.0758),
			new ComplexNumber(0.632, 0.0669),
			"СИП-2 3х95+1х70+1х25"
			);

		public static readonly СИП_2 _3х120_1х95_1х25 = new СИП_2(
			new ComplexNumber(0.325, 0.0745),
			new ComplexNumber(0.466, 0.065),
			"СИП-2 3х120+1х95+1х25"
			);

		public static IEnumerable<СИП_2> Values
		{
			get
			{
				yield return _3х35_1х54_1х25;
				yield return _3х50_1х54_1х25;
				yield return _3х70_1х54_1х25;
				yield return _3х95_1х70_1х25;
				yield return _3х120_1х95_1х25;
			}
		}
	}
}
