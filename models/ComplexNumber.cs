using System;
using System.Globalization;
using System.Numerics;
using Newtonsoft.Json;
using Complex64 = System.Numerics.Complex;

namespace PluginMatrixCalculation.models
{
    public class ComplexNumber
    {
        public double Real { get; set; }
        public double Imag { get; set; }

        public double Abs => Math.Sqrt(Math.Pow(Real, 2) + Math.Pow(Imag, 2));
        
        [JsonConstructor]
        public ComplexNumber(double real, double imag)
        {
            Real = real;
            Imag = imag;
        }

        public ComplexNumber(Complex64 complex64)
        {
            Real = complex64.Real;
            Imag = complex64.Imaginary;
        }

        public override string ToString()
        {
            return Math.Round(Abs, 2).ToString(CultureInfo.CurrentCulture);
        }

        public Complex64 ToComplex64()
		{
            return new Complex64(Real, Imag);

        }
    }
}