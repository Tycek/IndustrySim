using IndustrySim.Core.Markets;

namespace IndustrySim.UI.ViewModels;

public class ActiveContractViewModel : ViewModelBase
{
    public string Type            { get; }
    public string Resource        { get; }
    public string QuantityPerTurn { get; }
    public string PricePerUnit    { get; }
    public string TotalPerTurn    { get; }
    public string TurnsRemaining  { get; }
    public string Strikes         { get; }
    public string Status          { get; }
    public string Source          { get; }

    public ActiveContractViewModel(Contract contract, bool canFulfillThisTurn)
    {
        Type            = contract.Type == OfferType.Buy ? "Buy" : "Sell";
        Resource        = contract.ResourceName;
        QuantityPerTurn = contract.QuantityPerTurn.ToString("N0");
        PricePerUnit    = $"${contract.PricePerUnit:N2}";
        TotalPerTurn    = $"${contract.TotalPerTurn:N2}";
        TurnsRemaining  = $"{contract.TurnsRemaining} turns";
        Strikes         = contract.Strikes > 0 ? $"{contract.Strikes} / 3" : "—";
        Status          = canFulfillThisTurn ? "On Track" : "At Risk";
        Source          = contract.Source;
    }
}
