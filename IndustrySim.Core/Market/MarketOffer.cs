namespace IndustrySim.Core.Markets;

public class MarketOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public OfferType Type { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public decimal PricePerUnit { get; set; }

    /// <summary>Turns remaining before this offer expires. Decremented each turn; removed at 0.</summary>
    public int TurnsRemaining { get; set; }

    /// <summary>"Market" for game-generated offers; the company name for AI-posted offers.</summary>
    public string Source { get; set; } = "Market";

    public decimal TotalPrice => (decimal)Quantity * PricePerUnit;
}
