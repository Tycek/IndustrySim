using IndustrySim.Core.AiCompanies;
using IndustrySim.Core.Game;
using IndustrySim.Core.Industries;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.Tests;

/// <summary>
/// Tests for the player posting their own market offers and contracts,
/// including pre-commitment, cancellation refunds, and the bilateral credit
/// path when an AI company accepts a player-posted offer.
/// </summary>
public class PlayerPostingTests
{
    private static GameLoop MakeLoop(decimal balance = 10_000m)
    {
        var state = new GameState
        {
            Player = new Player { Name = "Player", Balance = balance }
        };
        return new GameLoop(state);
    }

    // ── TryPostOffer — Sell (player sells resources) ──────────────────────────

    [Fact]
    public void PostSellOffer_DeductsResourcesImmediately()
    {
        var loop = MakeLoop();
        loop.State.Player.Inventory[ResourceNames.Coal] = 50;

        loop.TryPostOffer(OfferType.Sell, ResourceNames.Coal, quantity: 30, pricePerUnit: 6m, turnsRemaining: 5);

        Assert.Equal(20, loop.State.Player.Inventory[ResourceNames.Coal]);
    }

    [Fact]
    public void PostSellOffer_AppearsOnMarketWithPlayerSource()
    {
        var loop = MakeLoop();
        loop.State.Player.Inventory[ResourceNames.Coal] = 50;

        loop.TryPostOffer(OfferType.Sell, ResourceNames.Coal, quantity: 30, pricePerUnit: 6m, turnsRemaining: 5);

        var offer = Assert.Single(loop.State.Market.Offers);
        Assert.Equal("Player",           offer.Source);
        Assert.Equal(OfferType.Sell,     offer.Type);
        Assert.Equal(ResourceNames.Coal, offer.ResourceName);
        Assert.Equal(30,                 offer.Quantity);
        Assert.Equal(6m,                 offer.PricePerUnit);
    }

    [Fact]
    public void PostSellOffer_InsufficientResources_ReturnsFalseAndInventoryUnchanged()
    {
        var loop = MakeLoop();
        loop.State.Player.Inventory[ResourceNames.Coal] = 10;

        var result = loop.TryPostOffer(OfferType.Sell, ResourceNames.Coal, quantity: 30, pricePerUnit: 6m, turnsRemaining: 5);

        Assert.False(result);
        Assert.Empty(loop.State.Market.Offers);
        Assert.Equal(10, loop.State.Player.Inventory[ResourceNames.Coal]);
    }

    // ── TryPostOffer — Buy (player buys resources) ────────────────────────────

    [Fact]
    public void PostBuyOffer_DeductsMoneyImmediately()
    {
        var loop = MakeLoop(balance: 10_000m);

        loop.TryPostOffer(OfferType.Buy, ResourceNames.Coal, quantity: 40, pricePerUnit: 5m, turnsRemaining: 5); // total $200

        Assert.Equal(9_800m, loop.State.Player.Balance);
    }

    [Fact]
    public void PostBuyOffer_AppearsOnMarketWithPlayerSource()
    {
        var loop = MakeLoop();

        loop.TryPostOffer(OfferType.Buy, ResourceNames.Coal, quantity: 40, pricePerUnit: 5m, turnsRemaining: 5);

        var offer = Assert.Single(loop.State.Market.Offers);
        Assert.Equal("Player",      offer.Source);
        Assert.Equal(OfferType.Buy, offer.Type);
    }

    [Fact]
    public void PostBuyOffer_InsufficientFunds_ReturnsFalseAndBalanceUnchanged()
    {
        var loop = MakeLoop(balance: 100m);

        var result = loop.TryPostOffer(OfferType.Buy, ResourceNames.Coal, quantity: 40, pricePerUnit: 5m, turnsRemaining: 5); // $200

        Assert.False(result);
        Assert.Empty(loop.State.Market.Offers);
        Assert.Equal(100m, loop.State.Player.Balance);
    }

    // ── TryCancelOffer — refund on cancellation ───────────────────────────────

    [Fact]
    public void CancelSellOffer_ResourcesRefundedToPlayer()
    {
        var loop = MakeLoop();
        loop.State.Player.Inventory[ResourceNames.Coal] = 50;
        loop.TryPostOffer(OfferType.Sell, ResourceNames.Coal, quantity: 30, pricePerUnit: 6m, turnsRemaining: 5);
        var offer = loop.State.Market.Offers.Single(o => o.Source == "Player");

        loop.TryCancelOffer(offer);

        Assert.Equal(50, loop.State.Player.Inventory[ResourceNames.Coal]);
        Assert.Empty(loop.State.Market.Offers);
    }

    [Fact]
    public void CancelBuyOffer_MoneyRefundedToPlayer()
    {
        var loop = MakeLoop(balance: 10_000m);
        loop.TryPostOffer(OfferType.Buy, ResourceNames.Coal, quantity: 40, pricePerUnit: 5m, turnsRemaining: 5); // $200 deducted
        var offer = loop.State.Market.Offers.Single(o => o.Source == "Player");

        loop.TryCancelOffer(offer);

        Assert.Equal(10_000m, loop.State.Player.Balance);
        Assert.Empty(loop.State.Market.Offers);
    }

    [Fact]
    public void CancelOffer_ReturnsFalseWhenNotInMarket()
    {
        var loop  = MakeLoop();
        var ghost = new MarketOffer { Type = OfferType.Sell, Source = "Player",
                                      ResourceName = ResourceNames.Coal,
                                      Quantity = 10, PricePerUnit = 5m, TurnsRemaining = 5 };

        var result = loop.TryCancelOffer(ghost);

        Assert.False(result);
    }

    // ── PostContract / TryCancelContract ─────────────────────────────────────

    [Fact]
    public void PostContract_AppearsInAvailableContractsWithPlayerSource()
    {
        var loop = MakeLoop();

        loop.PostContract(OfferType.Sell, ResourceNames.Coal,
            quantityPerTurn: 10, pricePerUnit: 5m, durationTurns: 8);

        var contract = Assert.Single(loop.State.Market.AvailableContracts);
        Assert.Equal("Player",           contract.Source);
        Assert.Equal(OfferType.Sell,     contract.Type);
        Assert.Equal(ResourceNames.Coal, contract.ResourceName);
        Assert.Equal(10,                 contract.QuantityPerTurn);
        Assert.Equal(5m,                 contract.PricePerUnit);
        Assert.Equal(8,                  contract.DurationTurns);
    }

    [Fact]
    public void CancelContract_RemovedFromAvailableContracts()
    {
        var loop = MakeLoop();
        loop.PostContract(OfferType.Sell, ResourceNames.Coal,
            quantityPerTurn: 10, pricePerUnit: 5m, durationTurns: 8);
        var contract = loop.State.Market.AvailableContracts.Single();

        var result = loop.TryCancelContract(contract);

        Assert.True(result);
        Assert.Empty(loop.State.Market.AvailableContracts);
    }

    [Fact]
    public void CancelContract_ReturnsFalseWhenAlreadyGone()
    {
        var loop  = MakeLoop();
        var ghost = new Contract { Source = "Player", Type = OfferType.Sell,
                                   ResourceName = ResourceNames.Coal };

        Assert.False(loop.TryCancelContract(ghost));
    }

    // ── AI accepts player-posted Sell offer — bilateral credit ────────────────

    [Fact]
    public void PlayerSellOffer_AiWithCoalDeficitAcceptsIt_PlayerReceivesPayment()
    {
        // Give the AI a CokeOven (consumes 5 coal / turn) so ComputeNetSurplus returns
        // a coal deficit, guaranteeing it will accept a cheap coal Sell offer.
        var state = new GameState
        {
            Player = new Player { Name = "Player", Balance = 9_000m }
        };
        var loop = new GameLoop(state);
        var ai = new AiCompany
        {
            Name       = "Acme",
            Balance    = 10_000m,
            Industries = [new CokeOven()],
            Inventory  = new Dictionary<string, double>
            {
                [ResourceNames.Coal]     = 100, // AI has enough coal to run the oven
                [ResourceNames.CoalCoke] = 0,
            }
        };
        loop.State.AiCompanies.Add(ai);

        // Simulate pre-commitment: player posted 20 coal, resources removed already.
        loop.State.Player.Inventory[ResourceNames.Coal] = 0;
        var offer = new MarketOffer
        {
            Type           = OfferType.Sell,
            Source         = "Player",
            ResourceName   = ResourceNames.Coal,
            Quantity       = 20,
            PricePerUnit   = 5m,    // at base price — well within AI's 1.15× threshold
            TurnsRemaining = 5,
        };
        loop.State.Market.Offers.Add(offer);

        loop.ProcessTurn();

        // AI will always accept: coal deficit exists, price OK, balance OK.
        Assert.DoesNotContain(offer, loop.State.Market.Offers);
        Assert.Equal(9_100m, loop.State.Player.Balance); // $9000 + $100 credit
    }
}
