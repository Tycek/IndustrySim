using IndustrySim.Core.Models;

namespace IndustrySim.Core.Markets;

/// <summary>
/// Holds the active one-time market offers. Each turn, all offers are decremented and
/// expired ones are removed, then a random batch of new offers is added.
/// New offers are priced relative to <see cref="PriceIndex"/>, which drifts with supply/demand.
/// </summary>
public class Market
{
    public static readonly IReadOnlyDictionary<string, decimal> BasePrices = new Dictionary<string, decimal>
    {
        [ResourceNames.Coal]       = 5m,
        [ResourceNames.IronOre]    = 8m,
        [ResourceNames.CoalCoke]   = 15m,
        [ResourceNames.SteelIngot] = 40m,
        [ResourceNames.SteelWire]   = 28m,
        [ResourceNames.SteelBar]    = 55m,
        [ResourceNames.SteelPlate]  = 90m,
        [ResourceNames.CopperOre]       = 7m,
        [ResourceNames.CopperIngot]     = 35m,
        [ResourceNames.CopperWire]      = 24m,
        [ResourceNames.CopperPipe]      = 45m,
        [ResourceNames.CrudeOil]        = 6m,
        [ResourceNames.Gas]             = 8m,
        [ResourceNames.Diesel]          = 10m,
        [ResourceNames.Kerosene]        = 9m,
        [ResourceNames.Chemicals]       = 12m,
        [ResourceNames.PlasticResin]    = 14m,
        [ResourceNames.Fertiliser]      = 22m,
        [ResourceNames.SyntheticRubber] = 32m,
        [ResourceNames.Plastics]        = 18m,
    };

    private const decimal MaxDriftPerTurn  = 0.03m; // 3 % per turn maximum move
    private const decimal PriceFloor       = 0.40m; // floor = 40 % of base
    private const decimal PriceCeiling     = 5.00m; // ceiling = 500 % of base

    private static readonly string[] Resources = [.. BasePrices.Keys];

    public List<MarketOffer> Offers             { get; set; } = [];
    public List<Contract>    AvailableContracts { get; set; } = [];

    /// <summary>Running "fair price" per resource. Drifts with offer/contract imbalance.</summary>
    public Dictionary<string, decimal> PriceIndex { get; set; } =
        new(BasePrices.ToDictionary(kv => kv.Key, kv => kv.Value));

    /// <summary>Snapshot of <see cref="PriceIndex"/> from the previous turn, used for trend arrows.</summary>
    public Dictionary<string, decimal> PreviousPriceIndex { get; set; } =
        new(BasePrices.ToDictionary(kv => kv.Key, kv => kv.Value));

    /// <summary>
    /// Adjusts <see cref="PriceIndex"/> based on the ratio of sell to buy pressure.
    /// Prices drift up when buy pressure exceeds sell (ratio &lt; 0.8), down when sell exceeds buy
    /// (ratio &gt; 1.2), and mean-revert toward base price when balanced.
    /// Clamped to [basePrice × 0.40, basePrice × 2.50].
    /// </summary>
    public void AdjustPrices(Dictionary<string, double> sellPressure, Dictionary<string, double> buyPressure)
    {
        PreviousPriceIndex = new Dictionary<string, decimal>(PriceIndex);

        foreach (var resource in BasePrices.Keys)
        {
            var sell      = sellPressure.GetValueOrDefault(resource);
            var buy       = buyPressure.GetValueOrDefault(resource);
            var current   = PriceIndex[resource];
            var basePrice = BasePrices[resource];

            var ratio = (sell == 0 && buy == 0) ? 1.0 : (buy == 0 ? double.MaxValue : sell / buy);

            decimal drift;
            if (ratio > 1.2)
                drift = -(current * MaxDriftPerTurn);
            else if (ratio < 0.8)
                drift = current * MaxDriftPerTurn;
            else
                drift = (basePrice - current) * (MaxDriftPerTurn / 2);

            var newPrice = current + drift;
            newPrice = Math.Clamp(newPrice, basePrice * PriceFloor, basePrice * PriceCeiling);
            PriceIndex[resource] = Math.Round(newPrice, 2);
        }
    }

    /// <summary>
    /// Decrements <see cref="MarketOffer.TurnsRemaining"/> on all offers, removes expired
    /// ones, then adds 2–5 new offers for randomly chosen resources and types.
    /// Also ticks available contracts and occasionally generates a new one.
    /// New offers last 3–7 turns. Prices are based on <see cref="PriceIndex"/>.
    /// Returns the non-Market offers that expired this tick so callers can refund pre-committed funds.
    /// </summary>
    public IReadOnlyList<MarketOffer> GenerateOffers(Random rng)
    {
        // One-time offers
        foreach (var offer in Offers)
            offer.TurnsRemaining--;
        var expired = Offers.Where(o => o.TurnsRemaining <= 0 && o.Source != "Market").ToList();
        Offers.RemoveAll(o => o.TurnsRemaining <= 0);

        var newOfferCount = rng.Next(2, 6);
        for (var i = 0; i < newOfferCount; i++)
        {
            var resource = Resources[rng.Next(Resources.Length)];
            var type     = rng.Next(2) == 0 ? OfferType.Sell : OfferType.Buy;
            Offers.Add(MakeOffer(rng, type, resource, PriceIndex[resource]));
        }

        // Contracts
        foreach (var contract in AvailableContracts)
            contract.TurnsAvailable--;
        AvailableContracts.RemoveAll(c => c.TurnsAvailable <= 0);

        if (rng.Next(3) == 0) // ~33 % chance per turn
        {
            var resource = Resources[rng.Next(Resources.Length)];
            var type     = rng.Next(2) == 0 ? OfferType.Sell : OfferType.Buy;
            AvailableContracts.Add(MakeContract(rng, type, resource, PriceIndex[resource]));
        }

        return expired;
    }

    private static MarketOffer MakeOffer(Random rng, OfferType type, string resource, decimal indexPrice)
    {
        var priceMultiplier = 0.8 + rng.NextDouble() * 0.4; // ±20 %
        var quantity        = rng.Next(1, 11) * 10;          // 10, 20, … 100

        return new MarketOffer
        {
            Type           = type,
            ResourceName   = resource,
            Quantity       = quantity,
            PricePerUnit   = Math.Round(indexPrice * (decimal)priceMultiplier, 2),
            TurnsRemaining = rng.Next(3, 8),
        };
    }

    private static Contract MakeContract(Random rng, OfferType type, string resource, decimal indexPrice)
    {
        var priceMultiplier = 0.85 + rng.NextDouble() * 0.3; // 85–115 % of index

        return new Contract
        {
            Type            = type,
            ResourceName    = resource,
            QuantityPerTurn = rng.Next(1, 5) * 5,            // 5, 10, 15, or 20 per turn
            PricePerUnit    = Math.Round(indexPrice * (decimal)priceMultiplier, 2),
            DurationTurns   = rng.Next(5, 16),               // 5–15 turns
            TurnsAvailable  = rng.Next(3, 7),                // 3–6 turns to accept
        };
    }
}
