using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginMatrixCalculation.models
{
	public class ConsumerType
	{
		public readonly bool IsThreePhase;
		public readonly string Name;
		public readonly ComplexNumber Power = new ComplexNumber(6000, 1760);//new ComplexNumber(450, -131.25);

		private ConsumerType(bool isThreePhase, string name)
		{
			IsThreePhase = isThreePhase;
			Name = name;
		}

		public static readonly ConsumerType СИП_4_2х16 = new ConsumerType(false, "СИП-4 (2х16)");
		public static readonly ConsumerType СИП_4_4х16 = new ConsumerType(true, "СИП-4 (4х16)");
		public static readonly ConsumerType СИП_4_2х25 = new ConsumerType(false, "СИП-4 (2х25)");
		public static readonly ConsumerType СИП_4_4х25 = new ConsumerType(true, "СИП-4 (4х25)");


		public static IEnumerable<ConsumerType> Values
		{
			get
			{
				yield return СИП_4_2х16;
				yield return СИП_4_4х16;
				yield return СИП_4_2х25;
				yield return СИП_4_4х25;
			}
		}
	}
}
