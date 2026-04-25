namespace IndustrySim.Core.Markets;

public class MarketOffer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public OfferType Type { get; set; }
    public string ResourceName { get; set; } = string.Empty;
    public double Quantity { get; set; }
    public decimal PricePerUnit { get; set; }

    /// <summary>The turn number on which this offer expires and is removed from the market.</summary>
    public int ExpiresOnTurn { get; set; }

    public decimal TotalPrice => (decimal)Quantity * PricePerUnit;
}
