using System;
using System.Globalization;
using Newtonsoft.Json;

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
        
        public override string ToString()
        {
            return Math.Round(Abs, 2).ToString(CultureInfo.CurrentCulture);
        }
    }
}