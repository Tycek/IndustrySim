namespace IndustrySim.Core.Markets;

public class Contract
{
    public Guid      Id              { get; set; } = Guid.NewGuid();
    public OfferType Type            { get; set; }
    public string    ResourceName    { get; set; } = string.Empty;
    public double    QuantityPerTurn { get; set; }
    public decimal   PricePerUnit    { get; set; }

    /// <summary>Total contract length once accepted, in turns.</summary>
    public int DurationTurns  { get; set; }

    /// <summary>Turns left before this contract disappears from the market unaccepted.</summary>
    public int TurnsAvailable { get; set; }

    /// <summary>Turns remaining on an active (accepted) contract. Set to <see cref="DurationTurns"/> on acceptance.</summary>
    public int TurnsRemaining { get; set; }

    /// <summary>Number of turns the executor has failed to fulfil this contract. Cancels at 3.</summary>
    public int Strikes { get; set; }

    /// <summary>"Market" for game-generated contracts; the poster's name for bilateral contracts.</summary>
    public string Source { get; set; } = "Market";

    /// <summary>
    /// True for the mirror copy held by the contract poster. The mirror is for display only;
    /// the executor's copy drives all settlement. Mirrors are removed when the original expires or is cancelled.
    /// </summary>
    public bool IsCounterpartyView { get; set; }

    /// <summary>Links a mirror contract back to the executor's original contract ID.</summary>
    public Guid? OriginalContractId { get; set; }

    public decimal TotalPerTurn       => (decimal)QuantityPerTurn * PricePerUnit;
    public decimal CancellationPenalty => TotalPerTurn * 3;

    /// <summary>
    /// Creates the mirror contract held by the poster. The mirror has the opposite type
    /// (so the poster sees their obligation correctly) and is flagged as a counterparty view.
    /// </summary>
    public static Contract CreateMirror(Contract original, string executorName) => new()
    {
        Type               = original.Type == OfferType.Buy ? OfferType.Sell : OfferType.Buy,
        ResourceName       = original.ResourceName,
        QuantityPerTurn    = original.QuantityPerTurn,
        PricePerUnit       = original.PricePerUnit,
        DurationTurns      = original.DurationTurns,
        TurnsRemaining     = original.DurationTurns,
        Source             = executorName,
        IsCounterpartyView = true,
        OriginalContractId = original.Id,
    };
}
