using Newtonsoft.Json;
using System.Collections.Generic;

namespace PluginMatrixCalculation.models
{
    public class CalculationResultsModel
    {
        public long CalculationId { get; set; }
        public List<ComplexNumber> BranchesVoltage { get; set; }
        public List<ComplexNumber> BranchesCurrents { get; set; }
        public CalculationResultForCTS ResultFotCTS { get; set; }
        public List<CalculationResultForTower> ResultsForTowers { get; set; }

        [JsonConstructor]
        public CalculationResultsModel(
            List<ComplexNumber> branchesVoltage,
            List<ComplexNumber> branchesCurrent,
            CalculationResultForCTS resultFotCTS,
            List<CalculationResultForTower> resultsForTowers)
        {
            BranchesVoltage = branchesVoltage;
            BranchesCurrents = branchesCurrent;
            ResultFotCTS = resultFotCTS;
            ResultsForTowers = resultsForTowers;
        }
    }
}