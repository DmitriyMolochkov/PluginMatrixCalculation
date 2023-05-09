using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace PluginMatrixCalculation.models
{
    public class CalculationResultForTower
    {
        public string TowerNumber { get; set; }
        public ComplexNumber VoltagePhaseA { get; set; }
        public ComplexNumber VoltagePhaseB { get; set; }
        public ComplexNumber VoltagePhaseC { get; set; }

        public double MaxVoltageLoss =>
            (230.94011 - new List<ComplexNumber> { VoltagePhaseA, VoltagePhaseB, VoltagePhaseC }.Min(x => x.Abs)) / 230.94011 * 100;

        [JsonConstructor]
        public CalculationResultForTower(
            string towerNumber,
            ComplexNumber voltagePhaseA,
            ComplexNumber voltagePhaseB,
            ComplexNumber voltagePhaseC)
        {
            TowerNumber = towerNumber;
            VoltagePhaseA = voltagePhaseA;
            VoltagePhaseB = voltagePhaseB;
            VoltagePhaseC = voltagePhaseC;
        }
        
    }
}