using System.Collections.Generic;
using IndustrySim.Core.AiCompanies;
using IndustrySim.Core.Game;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.Tests;

public class AiIndustryBuildingTests
{
    private static Player MakePlayer() => new() { Name = "Player" };

    // ── No building when price is at or below threshold ───────────────────────

    [Fact]
    public void ConsiderBuildingIndustry_NoShortage_NeverBuilds()
    {
        var rng    = new Random(42);
        var ai     = new AiCompany { Name = "Test", Balance = 100_000m };
        var player = MakePlayer();

        // Prices at base — no shortage
        var priceIndex = new Dictionary<string, decimal>(Market.BasePrices);

        for (var i = 0; i < 100; i++)
            ai.ConsiderBuildingIndustry(priceIndex, [], player, rng);

        Assert.Empty(ai.Industries);
    }

    [Fact]
    public void ConsiderBuildingIndustry_PriceJustBelowThreshold_NeverBuilds()
    {
        var rng    = new Random(42);
        var ai     = new AiCompany { Name = "Test", Balance = 100_000m };
        var player = MakePlayer();

        // 139 % of base — just below the 140 % threshold
        var priceIndex = new Dictionary<string, decimal>(Market.BasePrices)
        {
            [ResourceNames.Coal] = Market.BasePrices[ResourceNames.Coal] * 1.39m
        };

        for (var i = 0; i < 100; i++)
            ai.ConsiderBuildingIndustry(priceIndex, [], player, rng);

        Assert.Empty(ai.Industries);
    }

    // ── Building requires sufficient balance ──────────────────────────────────

    [Fact]
    public void ConsiderBuildingIndustry_InsufficientBalance_NeverBuilds()
    {
        var rng    = new Random(42);
        var ai     = new AiCompany { Name = "Test", Balance = 0m };
        var player = MakePlayer();

        var priceIndex = new Dictionary<string, decimal>(Market.BasePrices)
        {
            [ResourceNames.Coal] = Market.BasePrices[ResourceNames.Coal] * 2.0m
        };

        for (var i = 0; i < 100; i++)
            ai.ConsiderBuildingIndustry(priceIndex, [], player, rng);

        Assert.Empty(ai.Industries);
    }

    // ── Building eventually happens when conditions are met ───────────────────

    [Fact]
    public void ConsiderBuildingIndustry_ShortageAndFunds_EventuallyBuilds()
    {
        var rng    = new Random(42);
        var ai     = new AiCompany { Name = "Test", Balance = 100_000m };
        var player = MakePlayer();

        // Coal is at 2× base — well above the 140 % threshold
        var priceIndex = new Dictionary<string, decimal>(Market.BasePrices)
        {
            [ResourceNames.Coal] = Market.BasePrices[ResourceNames.Coal] * 2.0m
        };

        // At 10 % probability per call, 200 attempts gives ~1 − 0.9^200 ≈ 100 % chance.
        for (var i = 0; i < 200 && ai.Industries.Count == 0; i++)
            ai.ConsiderBuildingIndustry(priceIndex, [], player, rng);

        Assert.NotEmpty(ai.Industries);
    }

    // ── Build cost is deducted ────────────────────────────────────────────────

    [Fact]
    public void ConsiderBuildingIndustry_WhenBuilt_DeductsBuildCost()
    {
        var rng     = new Random(42);
        var ai      = new AiCompany { Name = "Test", Balance = 100_000m };
        var initial = ai.Balance;
        var player  = MakePlayer();

        var priceIndex = new Dictionary<string, decimal>(Market.BasePrices)
        {
            [ResourceNames.Coal] = Market.BasePrices[ResourceNames.Coal] * 2.0m
        };

        while (ai.Industries.Count == 0)
            ai.ConsiderBuildingIndustry(priceIndex, [], player, rng);

        Assert.True(ai.Balance < initial);
        Assert.Equal(ai.Industries[0].BuildCost, initial - ai.Balance);
    }

    // ── Safety margin ─────────────────────────────────────────────────────────

    [Fact]
    public void ConsiderBuildingIndustry_BalanceTooLowForSafetyFactor_DoesNotBuild()
    {
        var rng    = new Random(42);
        var player = MakePlayer();

        // Coal Mine build cost is 500. Safety factor is 3×, so minimum balance is 1 500.
        // Set balance to just below 1 500 — should never build.
        var ai = new AiCompany { Name = "Test", Balance = 1_499m };

        var priceIndex = new Dictionary<string, decimal>(Market.BasePrices)
        {
            [ResourceNames.Coal] = Market.BasePrices[ResourceNames.Coal] * 2.0m
        };

        for (var i = 0; i < 200; i++)
            ai.ConsiderBuildingIndustry(priceIndex, [], player, rng);

        // If the only buildable industry with sufficient safety is one whose cost × 3 ≤ 1499,
        // the AI might still build it. We just verify that a CoalMine (cost 500, needs 1500)
        // is never chosen when balance is 1499.
        var builtCoalMines = ai.Industries.OfType<IndustrySim.Core.Industries.CoalMine>().Count();
        Assert.Equal(0, builtCoalMines);
    }
}
