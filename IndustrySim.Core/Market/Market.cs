using IndustrySim.Core.Models;

namespace IndustrySim.Core.Markets;

/// <summary>
/// Holds the active one-time market offers. Each turn, all offers are decremented and
/// expired ones are removed, then a random batch of new offers is added.
/// </summary>
public class Market
{
    private static readonly Dictionary<string, decimal> BasePrices = new()
    {
        [ResourceNames.Coal]       = 5m,
        [ResourceNames.IronOre]    = 8m,
        [ResourceNames.CoalCoke]   = 15m,
        [ResourceNames.SteelIngot] = 40m,
    };

    private static readonly string[] Resources = [.. BasePrices.Keys];

    public List<MarketOffer> Offers             { get; set; } = [];
    public List<Contract>   AvailableContracts { get; set; } = [];

    /// <summary>
    /// Decrements <see cref="MarketOffer.TurnsRemaining"/> on all offers, removes expired
    /// ones, then adds 2–5 new offers for randomly chosen resources and types.
    /// Also ticks available contracts and occasionally generates a new one.
    /// New offers last 3–7 turns.
    /// </summary>
    public void GenerateOffers(Random rng)
    {
        // One-time offers
        foreach (var offer in Offers)
            offer.TurnsRemaining--;
        Offers.RemoveAll(o => o.TurnsRemaining <= 0);

        var newOfferCount = rng.Next(2, 6);
        for (var i = 0; i < newOfferCount; i++)
        {
            var resource = Resources[rng.Next(Resources.Length)];
            var type     = rng.Next(2) == 0 ? OfferType.Sell : OfferType.Buy;
            Offers.Add(MakeOffer(rng, type, resource, BasePrices[resource]));
        }

        // Contracts
        foreach (var contract in AvailableContracts)
            contract.TurnsAvailable--;
        AvailableContracts.RemoveAll(c => c.TurnsAvailable <= 0);

        if (rng.Next(3) == 0) // ~33 % chance per turn
        {
            var resource = Resources[rng.Next(Resources.Length)];
            var type     = rng.Next(2) == 0 ? OfferType.Sell : OfferType.Buy;
            AvailableContracts.Add(MakeContract(rng, type, resource, BasePrices[resource]));
        }
    }

    private static MarketOffer MakeOffer(Random rng, OfferType type, string resource, decimal basePrice)
    {
        var priceMultiplier = 0.8 + rng.NextDouble() * 0.4; // ±20 %
        var quantity        = rng.Next(1, 11) * 10;          // 10, 20, … 100

        return new MarketOffer
        {
            Type           = type,
            ResourceName   = resource,
            Quantity       = quantity,
            PricePerUnit   = Math.Round(basePrice * (decimal)priceMultiplier, 2),
            TurnsRemaining = rng.Next(3, 8),
        };
    }

    private static Contract MakeContract(Random rng, OfferType type, string resource, decimal basePrice)
    {
        var priceMultiplier = 0.85 + rng.NextDouble() * 0.3; // 85–115 % of base
        var quantity        = rng.Next(1, 5) * 5;            // 5, 10, 15, or 20 per turn

        return new Contract
        {
            Type           = type,
            ResourceName   = resource,
            QuantityPerTurn = quantity,
            PricePerUnit   = Math.Round(basePrice * (decimal)priceMultiplier, 2),
            DurationTurns  = rng.Next(5, 16),                // 5–15 turns
            TurnsAvailable = rng.Next(3, 7),                 // 3–6 turns to accept
        };
    }
}
