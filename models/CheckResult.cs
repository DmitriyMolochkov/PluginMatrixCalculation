namespace PluginMatrixCalculation.models
{
    public class CheckResult
    {
        public string TowerNumber { get; set; }
        public double MaxVoltageLoss { get; set; }
        public bool IsValid { get; set; }
    }
}