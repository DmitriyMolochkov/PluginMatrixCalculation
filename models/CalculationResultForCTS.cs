using Newtonsoft.Json;

namespace PluginMatrixCalculation.models
{
    public class CalculationResultForCTS
    {
        public ComplexNumber CurrentPhaseA { get; set;}
        public ComplexNumber CurrentPhaseB { get; set;}
        public ComplexNumber CurrentPhaseC { get; set;}

        [JsonConstructor]
        public CalculationResultForCTS(
            ComplexNumber currentPhaseA,
            ComplexNumber currentPhaseB,
            ComplexNumber currentPhaseC)
        {
            CurrentPhaseA = currentPhaseA;
            CurrentPhaseB = currentPhaseB;
            CurrentPhaseC = currentPhaseC;
        }
    }
}

