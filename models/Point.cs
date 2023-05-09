using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluginMatrixCalculation.models
{
	public class Point : IEquatable<Point>
	{
		public double X { get; }
		public double Y { get; }
		public Point(double x, double y)
		{
			X = x;
			Y = y;
		}

		public void Deconstruct(out double _X, out double _Y)
		{
			_X = X;
			_Y = Y;
		}
		public override bool Equals(object obj)
		{
			if (obj == null || obj.GetType() != GetType())
			{
				return false;
			}
			var other = (Point)obj;

			return Equals(other);
		}
		public bool Equals(Point other)
		{
			return RoundСoordinate(X) == RoundСoordinate(other.X) && RoundСoordinate(Y) == RoundСoordinate(other.Y);
		}

		public override string ToString()
		{
			return $"Point {{ X = {RoundСoordinate(X)}, Y = {RoundСoordinate(Y)} }}";
		}

		public override int GetHashCode()
		{
			return ToString().GetHashCode();
		}

		protected static bool EqualOperator(Point left, Point right)
		{
			if (ReferenceEquals(left, null) ^ ReferenceEquals(right, null))
			{
				return false;
			}
			return ReferenceEquals(left, right) || left.Equals(right);
		}

		protected static bool NotEqualOperator(Point left, Point right)
		{
			return !(EqualOperator(left, right));
		}

		public static bool operator ==(Point one, Point two)
		{
			return EqualOperator(one, two);
		}

		public static bool operator !=(Point one, Point two)
		{
			return NotEqualOperator(one, two);
		}

		public static Point operator +(Point one, Point two)
		{
			return new Point(one.X + two.X, one.Y + two.Y);
		}

		public static Point operator -(Point one, Point two)
		{
			return new Point(one.X - two.X, one.Y - two.Y);
		}

		private static double RoundСoordinate(double N)
		{
			return Math.Round(N, 1);
		}
	}
}
