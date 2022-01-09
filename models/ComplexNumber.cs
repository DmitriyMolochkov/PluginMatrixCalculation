namespace acPlt.models
{
    public class ComplexNumber
    {
        private double Real { get; set; }
        private double Imag { get; set; }

        public ComplexNumber() {}
        public ComplexNumber(double real, double imag)
        {
            Real = real;
            Imag = imag;
        }
    }
}