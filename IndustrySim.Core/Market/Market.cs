using IndustrySim.Core.Models;

namespace IndustrySim.Core.Markets;

/// <summary>
/// Holds the shared market state: one persistent buy offer per market-demanded resource
/// (capacity-limited by the market's own stockpile), AI/player-posted one-time offers,
/// and available contracts. Prices drift via AdjustPrices, driven by a combination of
/// actual offer activity and the stockpile fill ratio for market-demanded resources.
/// </summary>
public class Market
{
    public static readonly IReadOnlyDictionary<string, decimal> BasePrices = new Dictionary<string, decimal>
    {
        [ResourceNames.Coal]            = 5m,
        [ResourceNames.IronOre]         = 8m,
        [ResourceNames.CoalCoke]        = 15m,
        [ResourceNames.SteelIngot]      = 40m,
        [ResourceNames.SteelWire]       = 28m,
        [ResourceNames.SteelBar]        = 55m,
        [ResourceNames.SteelPlate]      = 90m,
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

    /// <summary>
    /// Resources the market demands externally. Only these have a stockpile and a persistent
    /// buy offer. Intermediate goods (Coal, Iron Ore, Crude Oil, etc.) are absent — their
    /// prices are driven purely by AI/player offer activity on the open market.
    /// Value = units consumed from the stockpile per turn (fixed baseline external demand).
    /// Also acts as the pressure scale for the fill-ratio price signal.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, double> ConsumptionRates =
        new Dictionary<string, double>
        {
            [ResourceNames.SteelIngot]      = 5,
            [ResourceNames.SteelWire]       = 4,
            [ResourceNames.SteelBar]        = 4,
            [ResourceNames.SteelPlate]      = 4,
            [ResourceNames.CopperIngot]     = 4,
            [ResourceNames.CopperWire]      = 3,
            [ResourceNames.CopperPipe]      = 3,
            [ResourceNames.Gas]             = 5,
            [ResourceNames.Diesel]          = 5,
            [ResourceNames.Kerosene]        = 5,
            [ResourceNames.Chemicals]       = 4,
            [ResourceNames.Plastics]        = 5,
            [ResourceNames.Fertiliser]      = 3,
            [ResourceNames.SyntheticRubber] = 3,
        };

    private const decimal MaxDriftPerTurn   = 0.03m; // 3 % per turn maximum move
    private const decimal PriceFloor        = 0.40m; // floor = 40 % of base
    private const decimal PriceCeiling      = 20.00m; // ceiling = 500 % of base
    private const double  GrowthRatePerTurn = 0.002; // 0.2 % capacity growth per turn
    private const double  CapacityMultiplier = 20.0; // starting capacity = rate × 10

    public List<MarketOffer> Offers             { get; set; } = [];
    public List<Contract>    AvailableContracts { get; set; } = [];

    /// <summary>Running "fair price" per resource. Drifts with supply/demand pressure.</summary>
    public Dictionary<string, decimal> PriceIndex { get; set; } =
        new(BasePrices.ToDictionary(kv => kv.Key, kv => kv.Value));

    /// <summary>Snapshot of <see cref="PriceIndex"/> from the previous turn, used for trend arrows.</summary>
    public Dictionary<string, decimal> PreviousPriceIndex { get; set; } =
        new(BasePrices.ToDictionary(kv => kv.Key, kv => kv.Value));

    /// <summary>Current stockpile level per market-demanded resource.</summary>
    public Dictionary<string, double> StockpileLevel    { get; set; } = [];

    /// <summary>Maximum stockpile capacity per resource. Grows slowly each turn.</summary>
    public Dictionary<string, double> StockpileCapacity { get; set; } = [];

    /// <summary>Active temporary events that shift stockpile levels each turn.</summary>
    public List<MarketEvent> ActiveEvents { get; set; } = [];

    // ── Initialisation ───────────────────────────────────────────────────────

    /// <summary>
    /// Randomises starting stockpile levels so each game begins with a unique price landscape.
    /// Call once from <see cref="GameLoop.StartNew"/> before the first turn.
    /// </summary>
    public void InitializeStockpiles(Random rng)
    {
        foreach (var (resource, rate) in ConsumptionRates)
        {
            var capacity = rate * CapacityMultiplier;
            StockpileCapacity[resource] = capacity;
            StockpileLevel[resource]    = capacity * (0.20 + rng.NextDouble() * 0.60);
        }
    }

    // ── Per-turn mechanics ───────────────────────────────────────────────────

    /// <summary>
    /// Advances stockpile state by one turn: applies baseline consumption, grows capacity,
    /// then applies and ticks active event shifts. Call before price adjustment each turn.
    /// </summary>
    public void TickStockpiles()
    {
        foreach (var (resource, rate) in ConsumptionRates)
        {
            StockpileLevel[resource]    = Math.Max(0, StockpileLevel.GetValueOrDefault(resource) - rate);
            StockpileCapacity[resource] = StockpileCapacity.GetValueOrDefault(resource) * (1 + GrowthRatePerTurn);
        }

        foreach (var evt in ActiveEvents.ToList())
        {
            var capacity = StockpileCapacity.GetValueOrDefault(evt.ResourceName);
            StockpileLevel[evt.ResourceName] = Math.Clamp(
                StockpileLevel.GetValueOrDefault(evt.ResourceName) + evt.StockpileShiftPerTurn,
                0, capacity);
            evt.TurnsRemaining--;
        }
        ActiveEvents.RemoveAll(e => e.TurnsRemaining <= 0);
    }

    /// <summary>
    /// Returns additional buy and sell pressure from the stockpile fill ratio for each
    /// market-demanded resource. Merge into offer-activity pressures before AdjustPrices.
    ///   Empty stockpile (fillRatio = 0) → extra buy pressure = ConsumptionRate, sell = 0.
    ///   Full  stockpile (fillRatio = 1) → extra sell pressure = ConsumptionRate, buy = 0.
    /// Intermediate goods (no entry in ConsumptionRates) contribute zero here.
    /// </summary>
    public (Dictionary<string, double> buy, Dictionary<string, double> sell) ComputeStockpilePressure()
    {
        var buy  = new Dictionary<string, double>();
        var sell = new Dictionary<string, double>();

        foreach (var (resource, rate) in ConsumptionRates)
        {
            var capacity = StockpileCapacity.GetValueOrDefault(resource);
            if (capacity <= 0) continue;

            var fill   = Math.Clamp(StockpileLevel.GetValueOrDefault(resource) / capacity, 0.0, 1.0);
            buy[resource]  = (1.0 - fill) * rate;
            sell[resource] = fill * rate;
        }

        return (buy, sell);
    }

    /// <summary>
    /// Increments the market stockpile for <paramref name="resourceName"/> by
    /// <paramref name="quantity"/>, clamped to capacity. Call whenever a market buy offer
    /// is accepted (player or AI sells to the market).
    /// </summary>
    public void RecordSaleToMarket(string resourceName, double quantity)
    {
        if (!StockpileLevel.ContainsKey(resourceName)) return;
        var cap = StockpileCapacity.GetValueOrDefault(resourceName);
        StockpileLevel[resourceName] = Math.Min(StockpileLevel[resourceName] + quantity, cap);
    }

    /// <summary>
    /// Rebuilds the one persistent buy offer per market-demanded resource using the current
    /// stockpile gap and updated <see cref="PriceIndex"/>. Omits resources whose stockpile
    /// is full. Call after <see cref="AdjustPrices"/> each turn.
    /// </summary>
    public void RefreshPersistentOffers()
    {
        Offers.RemoveAll(o => o.Source == "Market" && o.Type == OfferType.Buy);

        foreach (var (resource, rate) in ConsumptionRates)
        {
            var cap   = StockpileCapacity.GetValueOrDefault(resource);
            var level = StockpileLevel.GetValueOrDefault(resource);
            if (level >= cap) continue;

            var gap = cap - level;

            Offers.Add(new MarketOffer
            {
                Type           = OfferType.Buy,
                Source         = "Market",
                ResourceName   = resource,
                Quantity       = gap,
                PricePerUnit   = PriceIndex.GetValueOrDefault(resource, BasePrices.GetValueOrDefault(resource, 10m)),
                TurnsRemaining = int.MaxValue,
            });
        }
    }

    /// <summary>
    /// Ticks and expires non-market one-time offers, ticks available contracts, and
    /// occasionally posts a new market Buy contract. Returns expired non-market offers
    /// so the caller can refund pre-committed resources or money to their posters.
    /// </summary>
    public IReadOnlyList<MarketOffer> TickOffers(Random rng)
    {
        foreach (var offer in Offers.Where(o => o.Source != "Market"))
            offer.TurnsRemaining--;

        var expired = Offers.Where(o => o.TurnsRemaining <= 0 && o.Source != "Market").ToList();
        Offers.RemoveAll(o => o.TurnsRemaining <= 0 && o.Source != "Market");

        foreach (var contract in AvailableContracts)
            contract.TurnsAvailable--;
        AvailableContracts.RemoveAll(c => c.TurnsAvailable <= 0);

        return expired;
    }

    /// <summary>
    /// Adjusts <see cref="PriceIndex"/> based on the ratio of sell to buy pressure.
    /// Prices drift up when buy pressure exceeds sell (ratio &lt; 0.8), down when sell exceeds
    /// buy (ratio &gt; 1.2), and mean-revert toward base price when balanced.
    /// Clamped to [basePrice × 0.40, basePrice × 5.00].
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

    private static Contract MakeContract(Random rng, OfferType type, string resource, decimal indexPrice)
    {
        var priceMultiplier = 0.85 + rng.NextDouble() * 0.30; // 85–115 % of index

        return new Contract
        {
            Type            = type,
            ResourceName    = resource,
            QuantityPerTurn = rng.Next(1, 5) * 5,
            PricePerUnit    = Math.Round(indexPrice * (decimal)priceMultiplier, 2),
            DurationTurns   = rng.Next(5, 16),
            TurnsAvailable  = rng.Next(3, 7),
        };
    }
}
