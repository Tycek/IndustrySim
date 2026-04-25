namespace IndustrySim.Core.Markets;

public class Contract
{
    public Guid     Id              { get; set; } = Guid.NewGuid();
    public OfferType Type           { get; set; }
    public string   ResourceName    { get; set; } = string.Empty;
    public double   QuantityPerTurn { get; set; }
    public decimal  PricePerUnit    { get; set; }

    /// <summary>Total contract length once accepted, in turns.</summary>
    public int DurationTurns   { get; set; }

    /// <summary>Turns left before this contract disappears from the market unaccepted.</summary>
    public int TurnsAvailable  { get; set; }

    /// <summary>Turns remaining on an active (accepted) contract. Set to <see cref="DurationTurns"/> on acceptance.</summary>
    public int TurnsRemaining  { get; set; }

    public decimal TotalPerTurn => (decimal)QuantityPerTurn * PricePerUnit;
}
