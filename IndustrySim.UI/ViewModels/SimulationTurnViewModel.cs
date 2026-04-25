using IndustrySim.Core.Simulation;

namespace IndustrySim.UI.ViewModels;

public class SimulationTurnViewModel
{
    public string  Turn        { get; }
    public string  Revenue     { get; }
    public string  Costs       { get; }
    public string  Net         { get; }
    public string  Balance     { get; }
    public decimal NetValue    { get; }
    public decimal BalanceValue { get; }

    public SimulationTurnViewModel(SimulationTurnResult result)
    {
        NetValue     = result.Revenue - result.RunningCosts;
        BalanceValue = result.Balance;

        Turn    = result.Turn.ToString();
        Revenue = $"${result.Revenue:N2}";
        Costs   = $"${result.RunningCosts:N2}";
        Net     = $"${NetValue:N2}";
        Balance = $"${result.Balance:N2}";
    }
}
