using System.Collections.Generic;

namespace acPlt.models
{
    public class CalculationCreateModel
    {
        public List<List<int>> NetworkTopology { get; set; }
        public List<ComplexNumber> BranchResistances { get; set; }
        public List<ComplexNumber> BranchEVMs { get; set; }
        public List<ComplexNumber> BranchCurrentSources { get; set; }
        public List<string> TowersNumbers { get; set; }
        public List<string> CalculationPoints { get; set; }

        public CalculationCreateModel() { }
        
        public CalculationCreateModel(
            List<List<int>> networkTopology, 
            List<ComplexNumber> branchResistances, 
            List<ComplexNumber> branchEVMs, 
            List<ComplexNumber> branchCurrentSources, 
            List<string> towersNumbers, 
            List<string> calculationPoints)
        {
            NetworkTopology = networkTopology;
            BranchResistances = branchResistances;
            BranchEVMs = branchEVMs;
            BranchCurrentSources = branchCurrentSources;
            TowersNumbers = towersNumbers;
            CalculationPoints = calculationPoints;
        }
        
    }
}

