using System.Collections.Generic;
using System.Linq;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.Tests;

public class PriceIndexTests
{
    // ── AdjustPrices direction ────────────────────────────────────────────────

    [Fact]
    public void AdjustPrices_OnlySellPressure_DrivesIndexDown()
    {
        var market  = new Market();
        var initial = market.PriceIndex[ResourceNames.Coal];

        market.AdjustPrices(
            new() { [ResourceNames.Coal] = 100 },
            new());

        Assert.True(market.PriceIndex[ResourceNames.Coal] < initial);
    }

    [Fact]
    public void AdjustPrices_OnlyBuyPressure_DrivesIndexUp()
    {
        var market  = new Market();
        var initial = market.PriceIndex[ResourceNames.Coal];

        market.AdjustPrices(
            new(),
            new() { [ResourceNames.Coal] = 100 });

        Assert.True(market.PriceIndex[ResourceNames.Coal] > initial);
    }

    [Fact]
    public void AdjustPrices_BalancedPressure_DoesNotDrift()
    {
        var market  = new Market();
        var initial = market.PriceIndex[ResourceNames.Coal];

        // Equal sell and buy pressure → ratio = 1.0 → mean-reverts toward base
        // Starting at base, mean revert produces no change.
        market.AdjustPrices(
            new() { [ResourceNames.Coal] = 50 },
            new() { [ResourceNames.Coal] = 50 });

        // Price should not have moved (or moved only negligibly — it's exactly at base)
        Assert.Equal(initial, market.PriceIndex[ResourceNames.Coal]);
    }

    [Fact]
    public void AdjustPrices_ZeroPressure_MeanRevertsTowardBase()
    {
        var market    = new Market();
        var basePrice = Market.BasePrices[ResourceNames.Coal];

        // Push price above base
        market.PriceIndex[ResourceNames.Coal] = basePrice * 2m;
        var elevated = market.PriceIndex[ResourceNames.Coal];

        // No offers → balanced → should drift back toward base
        market.AdjustPrices(new(), new());

        Assert.True(market.PriceIndex[ResourceNames.Coal] < elevated);
        Assert.True(market.PriceIndex[ResourceNames.Coal] > basePrice); // hasn't reached base in one step
    }

    // ── Bounds ────────────────────────────────────────────────────────────────

    [Fact]
    public void AdjustPrices_PriceFloor_NeverBroken()
    {
        var market    = new Market();
        var basePrice = Market.BasePrices[ResourceNames.Coal];

        for (var i = 0; i < 200; i++)
            market.AdjustPrices(new() { [ResourceNames.Coal] = 1000 }, new());

        Assert.True(market.PriceIndex[ResourceNames.Coal] >= basePrice * 0.40m);
    }

    [Fact]
    public void AdjustPrices_PriceCeiling_NeverBroken()
    {
        var market    = new Market();
        var basePrice = Market.BasePrices[ResourceNames.Coal];

        for (var i = 0; i < 200; i++)
            market.AdjustPrices(new(), new() { [ResourceNames.Coal] = 1000 });

        Assert.True(market.PriceIndex[ResourceNames.Coal] <= basePrice * 5.00m);
    }

    // ── PreviousPriceIndex snapshot ───────────────────────────────────────────

    [Fact]
    public void AdjustPrices_RecordsPreviousBeforeUpdate()
    {
        var market  = new Market();
        var before  = market.PriceIndex[ResourceNames.Coal];

        market.AdjustPrices(new() { [ResourceNames.Coal] = 100 }, new());

        // Previous should hold the value from before this adjustment.
        Assert.Equal(before, market.PreviousPriceIndex[ResourceNames.Coal]);
        // And current should now differ.
        Assert.NotEqual(before, market.PriceIndex[ResourceNames.Coal]);
    }

    // ── RefreshPersistentOffers uses PriceIndex ───────────────────────────────

    [Fact]
    public void RefreshPersistentOffers_PricesOffersRelativeToPriceIndex()
    {
        var rng    = new Random(0);
        var market = new Market();
        market.InitializeStockpiles(rng);

        var resource  = ResourceNames.SteelIngot;
        var basePrice = Market.BasePrices[resource];

        // Set index to 2× base and refresh offers.
        market.PriceIndex[resource] = basePrice * 2m;
        market.RefreshPersistentOffers();

        var offer = market.Offers.FirstOrDefault(o => o.ResourceName == resource && o.Source == "Market");
        Assert.NotNull(offer);
        Assert.Equal(basePrice * 2m, offer.PricePerUnit);
    }

    // ── Per-resource independence ─────────────────────────────────────────────

    [Fact]
    public void AdjustPrices_OnlyAffectsTargetResource()
    {
        var market      = new Market();
        var coalBefore  = market.PriceIndex[ResourceNames.Coal];
        var ironBefore  = market.PriceIndex[ResourceNames.IronOre];

        // Apply sell pressure only to coal
        market.AdjustPrices(new() { [ResourceNames.Coal] = 100 }, new());

        Assert.True(market.PriceIndex[ResourceNames.Coal] < coalBefore);
        Assert.Equal(ironBefore, market.PriceIndex[ResourceNames.IronOre]);
    }
}
