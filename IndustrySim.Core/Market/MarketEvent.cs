namespace IndustrySim.Core.Markets;

public class MarketEvent
{
    public string ResourceName        { get; set; } = string.Empty;
    public double StockpileShiftPerTurn { get; set; }
    public int    TurnsRemaining      { get; set; }
    public string Description         { get; set; } = string.Empty;
}
