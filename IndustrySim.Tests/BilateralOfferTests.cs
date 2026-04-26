using IndustrySim.Core.AiCompanies;
using IndustrySim.Core.Game;
using IndustrySim.Core.Markets;
using IndustrySim.Core.Models;

namespace IndustrySim.Tests;

/// <summary>
/// Tests for bilateral one-time offer settlement.
/// The setup simulates the state AFTER an AI company posted an offer
/// (resources or money already pre-committed), which is what the market holds.
/// </summary>
public class BilateralOfferTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a GameLoop with a single AI company (no industries — it is a no-op
    /// during ProcessTurn so it never disturbs test assertions).
    /// </summary>
    private static (GameLoop loop, AiCompany ai) Setup(
        decimal playerBalance = 10_000m, decimal aiBalance = 5_000m)
    {
        var state = new GameState
        {
            Player = new Player { Name = "Player", Balance = playerBalance }
        };
        var loop = new GameLoop(state);
        var ai   = new AiCompany { Name = "Acme", Balance = aiBalance };
        loop.State.AiCompanies.Add(ai);
        return (loop, ai);
    }

    private static MarketOffer MakeSellOffer(string source, double qty, decimal pricePerUnit, int turns = 5)
        => new() { Type = OfferType.Sell, Source = source,
                   ResourceName = ResourceNames.Coal, Quantity = qty,
                   PricePerUnit = pricePerUnit, TurnsRemaining = turns };

    private static MarketOffer MakeBuyOffer(string source, double qty, decimal pricePerUnit, int turns = 5)
        => new() { Type = OfferType.Buy, Source = source,
                   ResourceName = ResourceNames.Coal, Quantity = qty,
                   PricePerUnit = pricePerUnit, TurnsRemaining = turns };

    // ── Player accepts AI Sell offer (AI posted; player buys) ─────────────────

    [Fact]
    public void AcceptAiSellOffer_PlayerReceivesResources()
    {
        var (loop, _) = Setup();
        var offer = MakeSellOffer("Acme", qty: 20, pricePerUnit: 5m); // total $100
        loop.State.Market.Offers.Add(offer);

        loop.TryAcceptOffer(offer);

        Assert.Equal(20, loop.State.Player.Inventory.GetValueOrDefault(ResourceNames.Coal));
    }

    [Fact]
    public void AcceptAiSellOffer_PlayerPaysFullPrice()
    {
        var (loop, _) = Setup(playerBalance: 10_000m);
        var offer = MakeSellOffer("Acme", qty: 20, pricePerUnit: 5m); // total $100
        loop.State.Market.Offers.Add(offer);

        loop.TryAcceptOffer(offer);

        Assert.Equal(9_900m, loop.State.Player.Balance);
    }

    [Fact]
    public void AcceptAiSellOffer_AiReceivesPayment()
    {
        var (loop, ai) = Setup(aiBalance: 5_000m);
        var offer = MakeSellOffer("Acme", qty: 20, pricePerUnit: 5m); // total $100
        loop.State.Market.Offers.Add(offer);

        loop.TryAcceptOffer(offer);

        Assert.Equal(5_100m, ai.Balance);
    }

    [Fact]
    public void AcceptAiSellOffer_OfferRemovedFromMarket()
    {
        var (loop, _) = Setup();
        var offer = MakeSellOffer("Acme", qty: 20, pricePerUnit: 5m);
        loop.State.Market.Offers.Add(offer);

        loop.TryAcceptOffer(offer);

        Assert.DoesNotContain(offer, loop.State.Market.Offers);
    }

    [Fact]
    public void AcceptAiSellOffer_InsufficientFunds_ReturnsFalseAndOfferStays()
    {
        var (loop, ai) = Setup(playerBalance: 50m);
        var offer = MakeSellOffer("Acme", qty: 20, pricePerUnit: 5m); // total $100 — unaffordable
        loop.State.Market.Offers.Add(offer);

        var result = loop.TryAcceptOffer(offer);

        Assert.False(result);
        Assert.Contains(offer, loop.State.Market.Offers);
        Assert.Equal(50m,     loop.State.Player.Balance); // unchanged
        Assert.Equal(5_000m,  ai.Balance);                // unchanged
    }

    // ── Player accepts AI Buy offer (AI posted; player sells) ─────────────────

    [Fact]
    public void AcceptAiBuyOffer_PlayerDeliversResources()
    {
        var (loop, _) = Setup();
        loop.State.Player.Inventory[ResourceNames.Coal] = 50;
        var offer = MakeBuyOffer("Acme", qty: 20, pricePerUnit: 6m); // total $120
        loop.State.Market.Offers.Add(offer);

        loop.TryAcceptOffer(offer);

        Assert.Equal(30, loop.State.Player.Inventory[ResourceNames.Coal]);
    }

    [Fact]
    public void AcceptAiBuyOffer_PlayerReceivesPayment()
    {
        var (loop, _) = Setup(playerBalance: 10_000m);
        loop.State.Player.Inventory[ResourceNames.Coal] = 50;
        var offer = MakeBuyOffer("Acme", qty: 20, pricePerUnit: 6m); // total $120
        loop.State.Market.Offers.Add(offer);

        loop.TryAcceptOffer(offer);

        Assert.Equal(10_120m, loop.State.Player.Balance);
    }

    [Fact]
    public void AcceptAiBuyOffer_AiReceivesResources()
    {
        var (loop, ai) = Setup();
        loop.State.Player.Inventory[ResourceNames.Coal] = 50;
        var offer = MakeBuyOffer("Acme", qty: 20, pricePerUnit: 6m);
        loop.State.Market.Offers.Add(offer);

        loop.TryAcceptOffer(offer);

        Assert.Equal(20, ai.Inventory.GetValueOrDefault(ResourceNames.Coal));
    }

    [Fact]
    public void AcceptAiBuyOffer_InsufficientInventory_ReturnsFalseAndOfferStays()
    {
        var (loop, _) = Setup();
        loop.State.Player.Inventory[ResourceNames.Coal] = 5; // need 20
        var offer = MakeBuyOffer("Acme", qty: 20, pricePerUnit: 6m);
        loop.State.Market.Offers.Add(offer);

        var result = loop.TryAcceptOffer(offer);

        Assert.False(result);
        Assert.Contains(offer, loop.State.Market.Offers);
        Assert.Equal(5, loop.State.Player.Inventory[ResourceNames.Coal]); // unchanged
    }

    // ── Offer expiry refunds (via ProcessTurn) ────────────────────────────────
    // TurnsRemaining = 1 → GenerateOffers decrements to 0 → expired → refunded.

    [Fact]
    public void AiSellOffer_Expiry_RefundsResourcesBackToAi()
    {
        // Simulates: AI pre-committed 30 coal when posting a Sell offer.
        var (loop, ai) = Setup();
        ai.Inventory[ResourceNames.Coal] = 0;
        var offer = MakeSellOffer("Acme", qty: 30, pricePerUnit: 5m, turns: 1);
        loop.State.Market.Offers.Add(offer);

        loop.ProcessTurn();

        Assert.Equal(30, ai.Inventory.GetValueOrDefault(ResourceNames.Coal));
        Assert.DoesNotContain(offer, loop.State.Market.Offers);
    }

    [Fact]
    public void AiBuyOffer_Expiry_RefundsMoneyBackToAi()
    {
        // Simulates: AI pre-committed $300 when posting a Buy offer (60 × $5).
        var (loop, ai) = Setup(aiBalance: 4_000m);
        var offer = MakeBuyOffer("Acme", qty: 60, pricePerUnit: 5m, turns: 1); // total $300
        loop.State.Market.Offers.Add(offer);

        loop.ProcessTurn();

        Assert.Equal(4_300m, ai.Balance); // $4000 + $300 refund
        Assert.DoesNotContain(offer, loop.State.Market.Offers);
    }

    [Fact]
    public void PlayerSellOffer_Expiry_RefundsResourcesBackToPlayer()
    {
        // Simulates: player pre-committed 25 coal when posting a Sell offer.
        var (loop, _) = Setup();
        loop.State.Player.Inventory[ResourceNames.Coal] = 0;
        var offer = MakeSellOffer("Player", qty: 25, pricePerUnit: 5m, turns: 1);
        loop.State.Market.Offers.Add(offer);

        loop.ProcessTurn();

        Assert.Equal(25, loop.State.Player.Inventory.GetValueOrDefault(ResourceNames.Coal));
    }

    [Fact]
    public void PlayerBuyOffer_Expiry_RefundsMoneyBackToPlayer()
    {
        // Simulates: player pre-committed $200 when posting a Buy offer (40 × $5).
        var (loop, _) = Setup(playerBalance: 7_000m);
        var offer = MakeBuyOffer("Player", qty: 40, pricePerUnit: 5m, turns: 1); // total $200
        loop.State.Market.Offers.Add(offer);

        loop.ProcessTurn();

        Assert.Equal(7_200m, loop.State.Player.Balance); // $7000 + $200 refund
    }

    [Fact]
    public void MarketOffer_Expiry_NoRefund()
    {
        // Market-generated offers have no pre-committed owner — nothing should be refunded.
        var (loop, ai) = Setup(playerBalance: 10_000m, aiBalance: 5_000m);
        var offer = MakeSellOffer("Market", qty: 50, pricePerUnit: 5m, turns: 1);
        loop.State.Market.Offers.Add(offer);

        loop.ProcessTurn();

        // Neither participant should gain resources.
        Assert.Equal(0,       loop.State.Player.Inventory.GetValueOrDefault(ResourceNames.Coal));
        Assert.Equal(0,       ai.Inventory.GetValueOrDefault(ResourceNames.Coal));
        Assert.Equal(5_000m,  ai.Balance);
    }
}
