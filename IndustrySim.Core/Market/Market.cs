using IndustrySim.Core.Models;

namespace IndustrySim.Core.Markets;

/// <summary>
/// Holds the active one-time market offers. Each turn, expired offers are removed and
/// a random batch of new offers is added for random resources.
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

    public List<MarketOffer> Offers { get; set; } = [];

    /// <summary>
    /// Removes expired offers, then adds 2–5 new offers for randomly chosen resources
    /// and types. Each new offer lasts 3–7 turns before expiring.
    /// </summary>
    public void GenerateOffers(Random rng, int currentTurn)
    {
        Offers.RemoveAll(o => o.ExpiresOnTurn <= currentTurn);

        var newOfferCount = rng.Next(2, 6);
        for (var i = 0; i < newOfferCount; i++)
        {
            var resource  = Resources[rng.Next(Resources.Length)];
            var type      = rng.Next(2) == 0 ? OfferType.Sell : OfferType.Buy;
            var lifespan  = rng.Next(3, 8); // expires in 3–7 turns

            Offers.Add(MakeOffer(rng, type, resource, BasePrices[resource], currentTurn + lifespan));
        }
    }

    private static MarketOffer MakeOffer(
        Random rng, OfferType type, string resource, decimal basePrice, int expiresOnTurn)
    {
        var priceMultiplier = 0.8 + rng.NextDouble() * 0.4; // ±20 %
        var quantity        = rng.Next(1, 11) * 10;          // 10, 20, … 100

        return new MarketOffer
        {
            Type          = type,
            ResourceName  = resource,
            Quantity      = quantity,
            PricePerUnit  = Math.Round(basePrice * (decimal)priceMultiplier, 2),
            ExpiresOnTurn = expiresOnTurn,
        };
    }
}
